﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Pathoschild.DesignByContract.Framework.Analysis
{
	/// <summary>Reflects methods and properties for contract analysis.</summary>
	[Serializable]
	public class MethodAnalyzer : IMethodAnalyzer
	{
		/*********
		** Accessors
		*********/
		/// <summary>The singleton instance.</summary>
		public static MethodAnalyzer Instance = new MethodAnalyzer();


		/*********
		** Public methods
		*********/
		/// <summary>Analyze the contract annotations on a methods.</summary>
		/// <param name="method">The method to analyze.</param>
		/// <param name="inheritContract">Whether to inherit attributes from base types or interfaces.</param>
		public MethodAnalysis AnalyzeMethod(MethodBase method, bool inheritContract)
		{
			// analyze method contract
			var parameterPreconditions = this.GetParameterPreconditions(method, inheritContract);
			var returnPreconditions = this.GetReturnValuePreconditions(method, inheritContract);

			// analyze property contract
			PropertyInfo property = this.GetProperty(method);
			if (property != null)
			{
				// cascade annotations on the property to the getter/setter methods
				if (this.IsPropertyGetter(method))
					returnPreconditions = returnPreconditions.Union(this.GetReturnValuePreconditions(property, inheritContract));
				else
					parameterPreconditions = parameterPreconditions.Union(this.GetParameterPreconditions(property, inheritContract));
			}

			// return analysis
			return new MethodAnalysis
			{
				ParameterPreconditions = parameterPreconditions.ToArray(),
				ReturnValuePreconditions = returnPreconditions.ToArray()
			};
		}


		/*********
		** Protected methods
		*********/
		/***
		** Method analysis
		***/
		/// <summary>Get whether a method returns a value.</summary>
		/// <param name="method">The method to analyze.</param>
		protected bool HasReturnValue(MethodBase method)
		{
			MethodInfo methodInfo = method as MethodInfo;
			return methodInfo != null && methodInfo.ReturnType != typeof(void);
		}

		/// <summary>Get whether a method is a property getter or setter.</summary>
		/// <param name="method">The method to analyze.</param>
		protected bool IsPropertyAccessor(MethodBase method)
		{
			MethodInfo methodInfo = method as MethodInfo;
			return methodInfo != null
				&& methodInfo.IsSpecialName
				&& (method.Name.StartsWith("get_") || method.Name.StartsWith("set_"));
		}

		/// <summary>Get whether the method is a property setter.</summary>
		/// <param name="method">The method to analyze.</param>
		protected bool IsPropertyGetter(MethodBase method)
		{
			return this.IsPropertyAccessor(method)
				&& method.Name.StartsWith("get_");
		}

		/// <summary>Get the property for which this method is an accessor.</summary>
		/// <param name="method">The method to analyze.</param>
		/// <returns>Returns the method's property, or <c>null</c> if it is not an accessor.</returns>
		protected PropertyInfo GetProperty(MethodBase method)
		{
			// analyze method
			if (!this.IsPropertyAccessor(method) || !(method is MethodInfo))
				return null;
			MethodInfo methodInfo = method as MethodInfo;
			bool isGet = this.IsPropertyGetter(methodInfo);

			// get matching property
			PropertyInfo[] properties = method.DeclaringType
				.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
				.Where(p => methodInfo == (isGet ? p.GetGetMethod(true) : p.GetSetMethod(true)))
				.ToArray();
			if (properties.Length <= 1)
				return properties.SingleOrDefault();

			// disambiguate between properties (e.g., hidden properties)
			PropertyInfo mostDerived = properties.First();
			foreach (PropertyInfo property in properties)
			{
				if (property.DeclaringType.IsSubclassOf(mostDerived.DeclaringType))
					mostDerived = property;
			}
			return mostDerived;
		}

		/***
		** Attributes
		***/
		/// <summary>Get the custom attributes of a given type from a provider.</summary>
		/// <typeparam name="T">The type of the custom attributes.</typeparam>
		/// <param name="customAttributeProvider">The object from which to retrieve custom attributes.</param>
		/// <param name="inherit">Whether to inherit attributes from base types or interfaces.</param>
		protected IEnumerable<T> GetCustomAttributes<T>(ICustomAttributeProvider customAttributeProvider, bool inherit)
		{
			return customAttributeProvider.GetCustomAttributes(typeof(T), inherit).Cast<T>();
		}

		/// <summary>Get the custom attributes of a given type from a provider.</summary>
		/// <typeparam name="T">The type of the custom attributes.</typeparam>
		/// <param name="property">The object from which to retrieve custom attributes.</param>
		/// <param name="inherit">Whether to inherit attributes from base types or interfaces.</param>
		protected IEnumerable<T> GetCustomAttributes<T>(MemberInfo property, bool inherit)
		{
			return property.GetCustomAttributes(typeof(T), inherit).Cast<T>();
		}

		/// <summary>Get the attributes applied to a method.</summary>
		/// <typeparam name="T">The attribute type to get.</typeparam>
		/// <param name="method">The method whose attributes to get.</param>
		/// <param name="inherit">Whether to inherit attributes from base types or interfaces.</param>
		/// <param name="withReturnTypeAttributes">Whether to include attributes restricted to its return type.</param>
		protected IEnumerable<T> GetMethodAttributes<T>(MethodInfo method, bool inherit, bool withReturnTypeAttributes = false)
		{
			var attributes = this.GetCustomAttributes<T>(method, inherit);
			if (withReturnTypeAttributes)
				attributes = this.GetCustomAttributes<T>(method.ReturnTypeCustomAttributes, inherit).Union(attributes);

			return attributes;
		}

		/// <summary>Get the attributes applied to a parameter.</summary>
		/// <typeparam name="T">The attribute type to get.</typeparam>
		/// <param name="parameter">The parameter whose attributes to get.</param>
		/// <param name="inherit">Whether to inherit attributes from base types or interfaces.</param>
		protected IEnumerable<T> GetParameterAttributes<T>(ParameterInfo parameter, bool inherit)
		{
			return this.GetCustomAttributes<T>(parameter, inherit);
		}

		/***
		** Preconditions
		***/
		/// <summary>Get the contract requirements for each method parameter or property setter value.</summary>
		/// <param name="method">The method to analyze.</param>
		/// <param name="inherit">Whether to inherit attributes from base types or interfaces.</param>
		protected IEnumerable<ParameterMetadata> GetParameterPreconditions(MethodBase method, bool inherit)
		{
			// get parameters
			// would have been a bit cleaner to do this in GetCustomAttributes but it'll be
			// more performant to run this once per method vs once per method param
			IEnumerable<ParameterInfo> parameters = method.GetParameters();
			if (inherit)
			{
				MethodInfo interfaceMethod = this.GetInterfaceDefinition(method) as MethodInfo;
				if (interfaceMethod != null)
					parameters = interfaceMethod.GetParameters().Union(parameters);
			}

			// select annotations
			return parameters.SelectMany(parameter => this.GetAnnotations(parameter, inherit));
		}
		
		/// <summary>Get the contract requirements for the property setter value.</summary>
		/// <param name="property">The property to analyze.</param>
		/// <param name="inherit">Whether to inherit attributes from base types or interfaces.</param>
		protected IEnumerable<ParameterMetadata> GetParameterPreconditions(PropertyInfo property, bool inherit)
		{
			if (!property.CanWrite)
				return new ParameterMetadata[0];

			IEnumerable<ParameterMetadata> annotations = this.GetAnnotations(property, inherit);
			if (inherit)
			{
				PropertyInfo interfaceProperty = this.GetInterfaceDefinition(property) as PropertyInfo;
				if (interfaceProperty != null)
					annotations = this.GetAnnotations(interfaceProperty, false).Union(annotations);
			}
			return annotations;
		}

		/// <summary>Get the contract requirements on a method or property return value.</summary>
		/// <param name="method">The method to analyze.</param>
		/// <param name="inherit">Whether to inherit attributes from base types or interfaces.</param>
		protected IEnumerable<ReturnValueMetadata> GetReturnValuePreconditions(MethodBase method, bool inherit)
		{
			if (!this.HasReturnValue(method))
				return new ReturnValueMetadata[0];

			IEnumerable<ReturnValueMetadata> annotations = this
				.GetMethodAttributes<IReturnValuePrecondition>(method as MethodInfo, inherit, true)
				.Select(attr => new ReturnValueMetadata(method, attr));
			if (inherit)
			{
				MethodInfo interfaceMethod = GetInterfaceDefinition(method) as MethodInfo;
				if (interfaceMethod != null)
					annotations = this
						.GetMethodAttributes<IReturnValuePrecondition>(interfaceMethod, false, true)
						.Select(attr => new ReturnValueMetadata(interfaceMethod, attr))
						.Union(annotations);
			}

			return annotations;
		}

		/// <summary>Get the contract requirements on a method or property return value.</summary>
		/// <param name="property">The method to analyze.</param>
		/// <param name="inherit">Whether to inherit attributes from base types or interfaces.</param>
		protected IEnumerable<ReturnValueMetadata> GetReturnValuePreconditions(PropertyInfo property, bool inherit)
		{
			IEnumerable<ReturnValueMetadata> annotations = this
				.GetCustomAttributes<IReturnValuePrecondition>(property, inherit)
				.Select(attr => new ReturnValueMetadata(property, attr));
			if (inherit)
			{
				MemberInfo interfaceMethod = GetInterfaceDefinition(property);
				if (interfaceMethod != null)
					annotations = this
						.GetCustomAttributes<IReturnValuePrecondition>(interfaceMethod, false)
						.Select(attr => new ReturnValueMetadata(interfaceMethod, attr))
						.Union(annotations);
			}

			return annotations;
		}

		/// <summary>Get contract annotations on a parameter.</summary>
		/// <param name="parameter">The parameter whose annotations to get.</param>
		/// <param name="inherit">Whether to inherit attributes from base types or interfaces.</param>
		private IEnumerable<ParameterMetadata> GetAnnotations(ParameterInfo parameter, bool inherit)
		{
			return this
				.GetParameterAttributes<IParameterPrecondition>(parameter, inherit)
				.Select(annotation => new ParameterMetadata(parameter, annotation));
		}

		/// <summary>Get contract annotations on a property.</summary>
		/// <param name="property">The property whose annotations to get.</param>
		/// <param name="inherit">Whether to inherit attributes from base types or interfaces.</param>
		private IEnumerable<ParameterMetadata> GetAnnotations(PropertyInfo property, bool inherit)
		{
			return this
				.GetCustomAttributes<IParameterPrecondition>(property, inherit)
				.Select(annotation =>
				{
					// setter value (implicit last parameter)
					ParameterInfo parameter = property.GetSetMethod(true).GetParameters().Last();
					return new ParameterMetadata(parameter, annotation, property.Name);
				});
		}

		/// <summary>Get the interface definition for an implemented method.</summary>
		/// <param name="member">The implemented method.</param>
		/// <returns>Returns the interface definition for an implemented method, or <c>null</c> if none was found.</returns>
		[CanBeNull]
		protected MemberInfo GetInterfaceDefinition(MemberInfo member)
		{
			Type methodType = member.ReflectedType;
			return methodType
				.GetInterfaces()
				.SelectMany(interfaceType => methodType.GetInterfaceMap(interfaceType).InterfaceMethods)
				.Where(m => this.MemberSignatureEquals(m, member))
				.Select(m =>
				{
					// if it's a prop getter/setter, return the property itself
					if (IsPropertyAccessor(m))
						return (MemberInfo)this.GetProperty(m);
					return m;
				})
				.Distinct()
				.SingleOrDefault();
		}

		/// <summary>Get whether two members have matching return types and parameters.</summary>
		/// <param name="member1">The method whose signature to compare.</param>
		/// <param name="member2">The other method whose signature to compare.</param>
		protected bool MemberSignatureEquals(MemberInfo member1, MemberInfo member2)
		{
			// get member names
			Func<MemberInfo, string> getName = member => member is MethodBase && IsPropertyAccessor(member as MethodBase)
				? this.GetProperty(member as MethodBase).Name
				: member.Name;
			string name1 = getName(member1);
			string name2 = getName(member2);

			// compare signatures
			return name1 == name2
				&& SelectivelyEquals<MethodBase>(m => m.GetParameters().Select(p => p.ParameterType), member1, member2) // ctors
				&& SelectivelyEquals<MethodInfo>(m => m.ReturnType, member1, member2) // methods
				&& SelectivelyEquals<FieldInfo>(f => f.FieldType, member1, member2) // fields
				&& SelectivelyEquals<PropertyInfo>(p => p.PropertyType, member1, member2); // properties
		}

		/// <summary>Compare two members by type and by a derived sequence of values.</summary>
		/// <typeparam name="TMember">The expected member type.</typeparam>
		/// <param name="select">Select the keys by which to compare the members.</param>
		/// <param name="member1">The method whose signature to compare.</param>
		/// <param name="member2">The other method whose signature to compare.</param>
		private static bool SelectivelyEquals<TMember>(Func<TMember, IEnumerable<Type>> select, MemberInfo member1, MemberInfo member2)
			where TMember : MemberInfo
		{
			if (!(member1 is TMember) || !(member2 is TMember))
				return true; // not applicable
			return select(member1 as TMember).SequenceEqual(select(member2 as TMember));
		}

		/// <summary>Compare two members by type and by a derived value.</summary>
		/// <typeparam name="TMember">The expected member type.</typeparam>
		/// <param name="select">Select the keys by which to compare the members.</param>
		/// <param name="member1">The method whose signature to compare.</param>
		/// <param name="member2">The other method whose signature to compare.</param>
		private static bool SelectivelyEquals<TMember>(Func<TMember, Type> select, MemberInfo member1, MemberInfo member2)
			where TMember : MemberInfo
		{
			if (!(member1 is TMember) || !(member2 is TMember))
				return true; // not applicable
			return select(member1 as TMember) == select(member2 as TMember);
		}
	}
}
