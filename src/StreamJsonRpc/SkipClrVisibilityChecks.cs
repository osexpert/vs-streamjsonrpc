﻿// Copyright (c) Microsoft Corporation. All rights reserved.

/*
 * This class was copied from https://github.com/AArnott/vs-mef/blob/master/src/Microsoft.VisualStudio.Composition/Reflection/SkipClrVisibilityChecks.cs
 * and should ideally be kept in sync.
 * Last updated from vs-mef commit: 8543110b32841ffca3cff659cbe2f2adabd93063
 */

namespace StreamJsonRpc
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Runtime.InteropServices;
    using Microsoft;

    /// <summary>
    /// Gives a dynamic assembly the ability to skip CLR visibility checks,
    /// allowing the assembly to access private members of another assembly.
    /// </summary>
    internal class SkipClrVisibilityChecks
    {
        /// <summary>
        /// The <see cref="Attribute.Attribute()"/> constructor.
        /// </summary>
        private static readonly ConstructorInfo AttributeBaseClassCtor = typeof(Attribute).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single(ctor => ctor.GetParameters().Length == 0);

        /// <summary>
        /// The <see cref="AttributeUsageAttribute(AttributeTargets)"/> constructor.
        /// </summary>
        private static readonly ConstructorInfo AttributeUsageCtor = typeof(AttributeUsageAttribute).GetConstructor(new Type[] { typeof(AttributeTargets) });

        /// <summary>
        /// The <see cref="AttributeUsageAttribute.AllowMultiple"/> property.
        /// </summary>
        private static readonly PropertyInfo AttributeUsageAllowMultipleProperty = typeof(AttributeUsageAttribute).GetProperty(nameof(AttributeUsageAttribute.AllowMultiple));

        /// <summary>
        /// The assembly builder that is constructing the dynamic assembly.
        /// </summary>
        private readonly AssemblyBuilder assemblyBuilder;

        /// <summary>
        /// The module builder for the default module of the <see cref="assemblyBuilder"/>.
        /// This is where the special attribute will be defined.
        /// </summary>
        private readonly ModuleBuilder moduleBuilder;

        /// <summary>
        /// The set of assemblies that already have visibility checks skipped for.
        /// </summary>
        private readonly HashSet<string> attributedAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The constructor on the special attribute to reference for each skipped assembly.
        /// </summary>
        private ConstructorInfo magicAttributeCtor;

        /// <summary>
        /// Initializes a new instance of the <see cref="SkipClrVisibilityChecks"/> class.
        /// </summary>
        /// <param name="assemblyBuilder">The builder for the dynamic assembly.</param>
        /// <param name="moduleBuilder">The builder for the default module defined by <see cref="assemblyBuilder"/>.</param>
        internal SkipClrVisibilityChecks(AssemblyBuilder assemblyBuilder, ModuleBuilder moduleBuilder)
        {
            Requires.NotNull(assemblyBuilder, nameof(assemblyBuilder));
            Requires.NotNull(moduleBuilder, nameof(moduleBuilder));
            this.assemblyBuilder = assemblyBuilder;
            this.moduleBuilder = moduleBuilder;
        }

        /// <summary>
        /// Gets the set of assemblies that a generated assembly must be granted the ability to skip visiblity checks for
        /// in order to access the specified type.
        /// </summary>
        /// <param name="typeInfo">The type which may be internal.</param>
        /// <returns>The set of names of assemblies to skip visibility checks for.</returns>
        internal static ImmutableHashSet<AssemblyName> GetSkipVisibilityChecksRequirements(TypeInfo typeInfo)
        {
            Requires.NotNull(typeInfo, nameof(typeInfo));

            if (IsPublic(typeInfo.AsType()))
            {
                return ImmutableHashSet<AssemblyName>.Empty;
            }
            else
            {
                ImmutableHashSet<AssemblyName> result = ImmutableHashSet<AssemblyName>.Empty
                    .Add(typeInfo.Assembly.GetName());

                // If this type has a base type defined in another assembly that is also internal
                // (with InternalsVisibleTo to the assembly hosting the original type)
                // then we'll need to add that.
                // TODO: Learn why somehow we don't need to do this, even given our test defines an internal interface
                //       that derives from another internal interface defined in another assembly.
                ////if (typeInfo.BaseType != null)
                ////{
                ////    result = result.Union(GetSkipVisibilityChecksRequirements(typeInfo.BaseType.GetTypeInfo()));
                ////}

                ////foreach (var iface in typeInfo.GetInterfaces())
                ////{
                ////    result = result.Union(GetSkipVisibilityChecksRequirements(iface.GetTypeInfo()));
                ////}

                return result;
            }
        }

        /// <summary>
        /// Add attributes to a dynamic assembly so that the CLR will skip visibility checks
        /// for the assemblies with the specified names.
        /// </summary>
        /// <param name="assemblyNames">The names of the assemblies to skip visibility checks for.</param>
        internal void SkipVisibilityChecksFor(IEnumerable<AssemblyName> assemblyNames)
        {
            Requires.NotNull(assemblyNames, nameof(assemblyNames));

            foreach (AssemblyName assemblyName in assemblyNames)
            {
                this.SkipVisibilityChecksFor(assemblyName);
            }
        }

        /// <summary>
        /// Add an attribute to a dynamic assembly so that the CLR will skip visibility checks
        /// for the assembly with the specified name.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly to skip visibility checks for.</param>
        internal void SkipVisibilityChecksFor(AssemblyName assemblyName)
        {
            Requires.NotNull(assemblyName, nameof(assemblyName));

            string assemblyNameArg = assemblyName.Name;
            if (this.attributedAssemblyNames.Add(assemblyNameArg))
            {
                var cab = new CustomAttributeBuilder(this.GetMagicAttributeCtor(), new object[] { assemblyNameArg });
                this.assemblyBuilder.SetCustomAttribute(cab);
            }
        }

        private static bool IsPublic(Type type, bool checkGenericTypeArgs = false)
        {
            Requires.NotNull(type, nameof(type));

            TypeInfo typeInfo = type.GetTypeInfo();
            if (typeInfo.IsNotPublic)
            {
                return false;
            }

            if (typeInfo.IsArray)
            {
                return IsPublic(typeInfo.GetElementType(), checkGenericTypeArgs);
            }

            if (checkGenericTypeArgs && typeInfo.IsGenericType && !typeInfo.IsGenericTypeDefinition)
            {
                // We have to treat embedded types that appear as generic type arguments as non-public,
                // because the CLR cannot assign Outer<TEmbedded> to Outer<TEmbedded> across assembly boundaries.
                if (typeInfo.GenericTypeArguments.Any(t => !IsPublic(t, true) || IsEmbeddedType(t)))
                {
                    return false;
                }
            }

            if (typeInfo.IsPublic || typeInfo.IsNestedPublic)
            {
                return true;
            }

            return false;
        }

        private static bool IsEmbeddedType(Type type)
        {
            Requires.NotNull(type, nameof(type));
            TypeInfo typeInfo = type.GetTypeInfo();

            if (typeInfo.IsInterface)
            {
                // TypeIdentifierAttribute signifies an embeddED type.
                // ComImportAttribute suggests an embeddABLE type.
                if (typeInfo.IsDefined(typeof(TypeIdentifierAttribute)) && typeInfo.IsDefined(typeof(GuidAttribute)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the constructor to the IgnoresAccessChecksToAttribute, generating the attribute if necessary.
        /// </summary>
        /// <returns>The constructor to the IgnoresAccessChecksToAttribute.</returns>
        private ConstructorInfo GetMagicAttributeCtor()
        {
            if (this.magicAttributeCtor == null)
            {
                TypeInfo magicAttribute = this.EmitMagicAttribute();
                this.magicAttributeCtor = magicAttribute.GetConstructor(new Type[] { typeof(string) });
            }

            return this.magicAttributeCtor;
        }

        /// <summary>
        /// Defines the special IgnoresAccessChecksToAttribute type in the <see cref="moduleBuilder"/>.
        /// </summary>
        /// <returns>The generated attribute type.</returns>
        private TypeInfo EmitMagicAttribute()
        {
            TypeBuilder tb = this.moduleBuilder.DefineType(
                "System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute",
                TypeAttributes.NotPublic,
                typeof(Attribute));

            var attributeUsage = new CustomAttributeBuilder(
                AttributeUsageCtor,
                new object[] { AttributeTargets.Assembly },
                new PropertyInfo[] { AttributeUsageAllowMultipleProperty },
                new object[] { false });
            tb.SetCustomAttribute(attributeUsage);

            ConstructorBuilder cb = tb.DefineConstructor(
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.RTSpecialName,
                CallingConventions.Standard,
                new Type[] { typeof(string) });
            cb.DefineParameter(1, ParameterAttributes.None, "assemblyName");

            ILGenerator il = cb.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, AttributeBaseClassCtor);
            il.Emit(OpCodes.Ret);

            return tb.CreateTypeInfo();
        }
    }
}
