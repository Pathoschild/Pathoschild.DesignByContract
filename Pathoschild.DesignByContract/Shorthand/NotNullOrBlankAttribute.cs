﻿using System;
using Pathoschild.DesignByContract.Framework;

namespace Pathoschild.DesignByContract.Shorthand
{
	/// <summary>A contract precondition that a value not be <c>null</c> nor a string that consists entirely of whitespace.</summary>
	[AttributeUsage((AttributeTargets)(ConditionTargets.Parameter | ConditionTargets.ReturnValue))]
	[Serializable]
	public class NotNullOrBlankAttribute : NotBlankAttribute, IParameterPrecondition, IReturnValuePrecondition
	{
		/*********
		** Public methods
		*********/
		/// <summary>Validate the requirement on a single method parameter or property setter value.</summary>
		/// <param name="parameter">The parameter metadata.</param>
		/// <param name="value">The parameter value.</param>
		/// <exception cref="ArgumentNullException">The contract requirement was not met because the value is <c>null</c>.</exception>
		/// <exception cref="ArgumentException">The contract requirement was not met because the value is blank.</exception>
		public new void OnParameterPrecondition(ParameterMetadata parameter, object value)
		{
			if (value == null)
				throw new ArgumentNullException(parameter.Name, parameter.GetMessage("cannot be null"));
			base.OnParameterPrecondition(parameter, value);
		}

		/// <summary>Validate the requirement on a method or property return value.</summary>
		/// <param name="returnValue">The return value metadata.</param>
		/// <param name="value">The return value.</param>
		/// <exception cref="NullReferenceException">The contract requirement was not met because the value is <c>null</c>.</exception>
		/// <exception cref="InvalidOperationException">The contract requirement was not met because the value is blank.</exception>
		public new void OnReturnValuePrecondition(ReturnValueMetadata returnValue, object value)
		{
			if (value == null)
				throw new NullReferenceException(returnValue.GetMessage("cannot be null"));
			base.OnReturnValuePrecondition(returnValue, value);
		}
	}
}
