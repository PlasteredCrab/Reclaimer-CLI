﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace System.IO.Endian
{
    internal static class DynamicReader<T>
    {
        private delegate T DynamicRead(EndianReader reader, T instance);

        private static readonly Type TypeArg = typeof(T);
        private static readonly Type[] ReadArgs = new[] { typeof(EndianReader), typeof(T) };

        private static DynamicRead Unversioned;
        private static readonly ConcurrentDictionary<double, DynamicRead> VersionLookup = new ConcurrentDictionary<double, DynamicRead>();

        public static T Read(EndianReader reader, double? version, T instance)
        {
            if (TypeArg.IsPrimitive)
                return instance;

            if (version.HasValue && VersionLookup.ContainsKey(version.Value))
                return VersionLookup[version.Value](reader, instance);
            else if (Unversioned != null)
                return Unversioned(reader, instance);
            else return GenerateReadMethod(version)(reader, instance);
        }

        public static void DumpAssembly(double? version)
        {
            var fileName = $"{nameof(DynamicReader<T>)}.{TypeArg.Name}_{version}.dll";

            var asmName = new AssemblyName(nameof(DynamicReader<T>));
            var asmBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Save);
            var modBuilder = asmBuilder.DefineDynamicModule(nameof(DynamicReader<T>), fileName);
            var typBuilder = modBuilder.DefineType($"{nameof(DynamicReader<T>)}.GeneratedClass", TypeAttributes.Public);

            var metBuilder = typBuilder.DefineMethod("DynamicRead", MethodAttributes.Public | MethodAttributes.Static, TypeArg, ReadArgs);

            var il = metBuilder.GetILGenerator();
            GenerateRead(il, version);
            typBuilder.CreateType();
            asmBuilder.Save(fileName);
        }

        private static DynamicRead GenerateReadMethod(double? version)
        {
            if (Debugger.IsAttached)
                DumpAssembly(version);

            var method = new DynamicMethod($"Deserialize<{TypeArg.Name}>[{version}]", TypeArg, ReadArgs, typeof(DynamicReader<T>));

            var il = method.GetILGenerator();
            GenerateRead(il, version);
            var del = (DynamicRead)method.CreateDelegate(typeof(DynamicRead));

            if (version.HasValue)
                VersionLookup.TryAdd(version.Value, del);
            else Unversioned = del;

            return del;
        }

        private static IEnumerable<PropertyInfo> GetProperties(Type type, double? version)
        {
            return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => Utils.CheckPropertyForReadWrite(p, version))
                .OrderBy(p => Utils.GetAttributeForVersion<OffsetAttribute>(p, version).Offset);
        }

        private static void GenerateRead(ILGenerator il, double? version)
        {
            var typeOrder = Utils.GetAttributeForVersion<ByteOrderAttribute>(TypeArg, version);

            var begin = il.DeclareLocal(typeof(long));
            il.Emit(OpCodes.Ldarg_0); //reader
            il.Emit(OpCodes.Callvirt, typeof(EndianReader).GetProperty(nameof(EndianReader.BaseStream)).GetGetMethod());
            il.Emit(OpCodes.Callvirt, typeof(Stream).GetProperty(nameof(Stream.Position)).GetGetMethod());
            il.Emit(OpCodes.Stloc_S, begin);

            #region Read Properties
            foreach (var prop in GetProperties(TypeArg, version))
            {
                var offset = Utils.GetAttributeForVersion<OffsetAttribute>(prop, version);
                if (offset == null) continue;

                EmitSeek(il, begin, offset.Offset);

                var storeType = Utils.GetAttributeForVersion<StoreTypeAttribute>(prop, version)?.StoreType ?? prop.PropertyType;
                var isNullable = storeType.IsGenericType && storeType.GetGenericTypeDefinition().Equals(typeof(Nullable<>));
                var nullableType = isNullable ? storeType : null;

                if (isNullable)
                    storeType = storeType.GetGenericArguments()[0];

                if (storeType.IsEnum)
                    storeType = storeType.GetEnumUnderlyingType();

                if (TypeArg.IsValueType)
                    il.Emit(OpCodes.Ldarga_S, (byte)1);
                else il.Emit(OpCodes.Ldarg_1); // instance

                var propOrder = Utils.GetAttributeForVersion<ByteOrderAttribute>(prop, version);
                if (storeType.Equals(typeof(string)))
                    EmitStringRead(il, prop, propOrder?.ByteOrder ?? typeOrder?.ByteOrder);
                else if (storeType.IsPrimitive || storeType.Equals(typeof(Guid)))
                    EmitPrimitiveRead(il, storeType, propOrder?.ByteOrder ?? typeOrder?.ByteOrder);
                else
                {
                    var method = (from m in typeof(EndianReader).GetMethods()
                                  where m.Name == nameof(EndianReader.ReadObject)
                                  && m.IsGenericMethodDefinition
                                  let p = m.GetParameters()
                                  where (version.HasValue && p.Length == 1 && p[0].ParameterType == typeof(double))
                                  || (!version.HasValue && p.Length == 0)
                                  select m).Single().MakeGenericMethod(prop.PropertyType);

                    il.Emit(OpCodes.Ldarg_0); //reader
                    if (version.HasValue)
                        il.Emit(OpCodes.Ldc_R8, version.Value);
                    il.Emit(OpCodes.Callvirt, method);
                }

                if (isNullable)
                {
                    var ctor = nullableType.GetConstructor(new[] { nullableType.GetGenericArguments()[0] });
                    il.Emit(OpCodes.Newobj, ctor);
                }

                var setter = prop.GetSetMethod();

                if (TypeArg.IsValueType)
                    il.Emit(OpCodes.Call, setter);
                else il.Emit(OpCodes.Callvirt, setter);
            }
            #endregion

            #region DataLength Check
            var lengthProps = TypeArg.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(p => Attribute.IsDefined(p, typeof(DataLengthAttribute)))
                    .Where(p => Utils.GetAttributeForVersion<DataLengthAttribute>(p, version) != null);

            if (lengthProps.Count() > 1)
                throw Exceptions.MultipleDataLengthsSpecified(TypeArg.Name, version);

            var lengthProp = lengthProps.FirstOrDefault();
            if (lengthProp != null)
            {
                var endLenCheck = il.DefineLabel();

                if (!lengthProp.PropertyType.Equals(typeof(long)))
                {
                    var temp = il.DeclareLocal(typeof(object));
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Callvirt, lengthProp.GetGetMethod());
                    if (lengthProp.PropertyType.IsValueType)
                        il.Emit(OpCodes.Box, lengthProp.PropertyType);
                    il.Emit(OpCodes.Stloc_S, temp);

                    var tryConvert = typeof(Utils).GetMethod(nameof(Utils.TryConvert), BindingFlags.Static | BindingFlags.NonPublic);
                    il.Emit(OpCodes.Ldloca_S, temp);
                    EmitTypeOf(il, lengthProp.PropertyType);
                    EmitTypeOf(il, typeof(long));
                    il.Emit(OpCodes.Call, tryConvert);
                    il.Emit(OpCodes.Brfalse_S, endLenCheck);

                    il.Emit(OpCodes.Ldarg_0); //reader
                    il.Emit(OpCodes.Callvirt, typeof(EndianReader).GetProperty(nameof(EndianReader.BaseStream)).GetGetMethod());
                    il.Emit(OpCodes.Ldloc_S, begin);
                    il.Emit(OpCodes.Ldloc_S, temp);
                    il.Emit(OpCodes.Unbox_Any, typeof(long));
                    il.Emit(OpCodes.Add);
                }
                else
                {
                    il.Emit(OpCodes.Ldarg_0); //reader
                    il.Emit(OpCodes.Callvirt, typeof(EndianReader).GetProperty(nameof(EndianReader.BaseStream)).GetGetMethod());
                    il.Emit(OpCodes.Ldloc_S, begin);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Callvirt, lengthProp.GetGetMethod());
                    il.Emit(OpCodes.Add);
                }

                il.Emit(OpCodes.Callvirt, typeof(Stream).GetProperty(nameof(Stream.Position)).GetSetMethod());

                il.MarkLabel(endLenCheck);
            }
            #endregion

            var fixedSize = Utils.GetAttributeForVersion<FixedSizeAttribute>(TypeArg, version);
            if (fixedSize != null)
                EmitSeek(il, begin, fixedSize.Size);

            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ret);
        }

        private static void EmitTypeOf(ILGenerator il, Type type)
        {
            il.Emit(OpCodes.Ldtoken, type);
            il.Emit(OpCodes.Call, typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), new[] { typeof(RuntimeTypeHandle) }));
        }

        private static void EmitSeek(ILGenerator il, LocalBuilder begin, long offset)
        {
            il.Emit(OpCodes.Ldarg_0); //reader
            il.Emit(OpCodes.Callvirt, typeof(EndianReader).GetProperty(nameof(EndianReader.BaseStream)).GetGetMethod());
            il.Emit(OpCodes.Ldloc_S, begin);
            if (offset != 0)
            {
                if (offset > int.MaxValue)
                    il.Emit(OpCodes.Ldc_I8, offset);
                else
                {
                    il.Emit(OpCodes.Ldc_I4, (int)offset);
                    il.Emit(OpCodes.Conv_I8);
                }
                il.Emit(OpCodes.Add); //begin + offset
            }
            il.Emit(OpCodes.Callvirt, typeof(Stream).GetProperty(nameof(Stream.Position)).GetSetMethod());
        }
        private static void EmitStringRead(ILGenerator il, PropertyInfo prop, ByteOrder? order)
        {
            var lenPrefixed = Utils.GetCustomAttribute<LengthPrefixedAttribute>(prop);
            var fixedLen = Utils.GetCustomAttribute<FixedLengthAttribute>(prop);
            var nullTerm = Utils.GetCustomAttribute<NullTerminatedAttribute>(prop);

            if (lenPrefixed == null && fixedLen == null && nullTerm == null)
                throw Exceptions.StringTypeUnknown(prop.Name);

            il.Emit(OpCodes.Ldarg_0); //reader

            MethodInfo method = null;
            if (lenPrefixed != null)
            {
                method = typeof(EndianReader).GetMethod(nameof(EndianReader.ReadString), order.HasValue ? new[] { typeof(ByteOrder) } : Type.EmptyTypes);
                if (order.HasValue)
                    il.Emit(OpCodes.Ldc_I4, (int)order.Value);
            }

            if (fixedLen != null)
            {
                if (lenPrefixed != null)
                    throw Exceptions.StringTypeOverlap(prop.Name);

                method = typeof(EndianReader).GetMethod(nameof(EndianReader.ReadString), new[] { typeof(int), typeof(bool) });
                il.Emit(OpCodes.Ldc_I4, fixedLen.Length);
                il.Emit(OpCodes.Ldc_I4, Convert.ToInt32(fixedLen.Trim));
            }

            if (nullTerm != null)
            {
                if (lenPrefixed != null || fixedLen != null)
                    throw Exceptions.StringTypeOverlap(prop.Name);

                if (nullTerm.HasLength)
                {
                    method = typeof(EndianReader).GetMethod(nameof(EndianReader.ReadNullTerminatedString), new[] { typeof(int) });
                    il.Emit(OpCodes.Ldc_I4, nullTerm.Length);
                }
                else method = typeof(EndianReader).GetMethod(nameof(EndianReader.ReadNullTerminatedString), Type.EmptyTypes);
            }

            il.Emit(OpCodes.Callvirt, method);
        }

        private static void EmitPrimitiveRead(ILGenerator il, Type type, ByteOrder? order)
        {
            var primitiveMethod = (from m in typeof(EndianReader).GetMethods()
                                   where m.Name.StartsWith(nameof(EndianReader.Read), StringComparison.Ordinal)
                                   && !m.Name.Equals(nameof(EndianReader.Read), StringComparison.Ordinal)
                                   && m.ReturnType.Equals(type)
                                   let args = m.GetParameters()
                                   where (type.Equals(typeof(byte)) || type.Equals(typeof(sbyte)))
                                   || (!order.HasValue && args.Length == 0)
                                   || (order.HasValue && args.Length == 1 && args[0].ParameterType.Equals(typeof(ByteOrder)))
                                   select m).SingleOrDefault();

            if (primitiveMethod == null)
                throw Exceptions.MissingPrimitiveReadMethod(type.Name);

            il.Emit(OpCodes.Ldarg_0); //reader
            if (order.HasValue && !type.Equals(typeof(byte)) && !type.Equals(typeof(sbyte)))
                il.Emit(OpCodes.Ldc_I4, (int)order.Value);
            il.Emit(OpCodes.Callvirt, primitiveMethod);
        }
    }
}
