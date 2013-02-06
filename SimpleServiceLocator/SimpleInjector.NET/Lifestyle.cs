﻿#region Copyright (c) 2013 S. van Deursen
/* The Simple Injector is an easy-to-use Inversion of Control library for .NET
 * 
 * Copyright (C) 2013 S. van Deursen
 * 
 * To contact me, please visit my blog at http://www.cuttingedge.it/blogs/steven/ or mail to steven at 
 * cuttingedge.it.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and 
 * associated documentation files (the "Software"), to deal in the Software without restriction, including 
 * without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
 * copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the 
 * following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all copies or substantial 
 * portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT 
 * LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO 
 * EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER 
 * IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE 
 * USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
#endregion

namespace SimpleInjector
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq.Expressions;
    using System.Reflection;

    using SimpleInjector.Lifestyles;

    /// <summary>
    /// Instances returned from the container can be cached. This caching is called Object Lifetime Management.
    /// Instances can be cached indefinately using the <see cref="Singleton"/> lifestyle, or never using the
    /// <see cref="Transient"/> lifestyle. 
    /// </summary>
    [DebuggerDisplay("{Name,nq}")]
    public abstract class Lifestyle
    {
        /// <summary>
        /// The lifestyle instance that doesn't cache instances. A new instance of the specified
        /// component is created every time the registered service it is requested or injected.
        /// </summary>
        public static readonly Lifestyle Transient = new TransientLifestyle();

        /// <summary>
        /// The lifestyle that caches components during the lifetime of the <see cref="Container"/> instance
        /// and guarantees that only a single instance of that component is created for that instance. Since
        /// general use is to create a single <b>Container</b> instance for the lifetime of the application /
        /// AppDomain, this would mean that only a single instance of that component would exist during the
        /// lifetime of the application. In a multi-threaded applications, implementations registered using 
        /// this lifestyle must be thread-safe.
        /// </summary>
        public static readonly Lifestyle Singleton = new SingletonLifestyle();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal static readonly Lifestyle Unknown = new UnknownLifestyle();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly MethodInfo OpenCreateRegistrationTServiceTImplementationMethod =
            GetMethod(lifestyle => lifestyle.CreateRegistration<object, object>(null));

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly MethodInfo OpenCreateRegistrationTServiceFuncMethod =
            GetMethod(lifestyle => lifestyle.CreateRegistration<object>(null, null));

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly string name;

        /// <summary>Initializes a new instance of the <see cref="Lifestyle"/> class.</summary>
        /// <param name="name">The user friendly name of this lifestyle.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null (Nothing in VB) 
        /// or an empty string.</exception>
        protected Lifestyle(string name)
        {
            Requires.IsNotNullOrEmpty(name, "name");

            this.name = name;
        }

        /// <summary>Gets the user friendly name of this lifestyle.</summary>
        public string Name 
        { 
            get { return this.name; } 
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal virtual int ComponentLength 
        {
            get { return this.Length; }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal virtual int DependencyLength 
        {
            get { return this.Length; }
        }

        /// <summary>
        /// Gets the length of the lifestyle. Implementers must implement this property. The diagnostic
        /// services use this value to compare lifestyles with each other to determine lifestyle 
        /// misconfigurations.
        /// </summary>
        protected abstract int Length
        {
            get;
        }

        /// <summary>
        /// Creates a new <see cref="Registration"/> instance defining the creation of the
        /// specified <typeparamref name="TImplementation"/> with the caching as specified by this lifestyle.
        /// </summary>
        /// <typeparam name="TService">The interface or base type that can be used to retrieve the instances.</typeparam>
        /// <typeparam name="TImplementation">The concrete type that will be registered.</typeparam>
        /// <param name="container">The <see cref="Container"/> instance for which a 
        /// <see cref="Registration"/> must be created.</param>
        /// <returns>A new <see cref="Registration"/> instance.</returns>
        [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter",
            Justification = "Supplying the generic type arguments is needed, since internal types can not " + 
                            "be created using the non-generic overloads in a sandbox.")]
        public abstract Registration CreateRegistration<TService, TImplementation>(Container container)
            where TImplementation : class, TService
            where TService : class;

        /// <summary>
        /// Creates a new <see cref="Registration"/> instance defining the creation of the
        /// specified <typeparamref name="TService"/> using the supplied <paramref name="instanceCreator"/> 
        /// with the caching as specified by this lifestyle.
        /// </summary>
        /// <typeparam name="TService">The interface or base type that can be used to retrieve the instances.</typeparam>
        /// <param name="instanceCreator">A delegate that will create a new instance of 
        /// <typeparamref name="TService"/> every time it is called.</param>
        /// <param name="container">The <see cref="Container"/> instance for which a 
        /// <see cref="Registration"/> must be created.</param>
        /// <returns>A new <see cref="Registration"/> instance.</returns>
        public abstract Registration CreateRegistration<TService>(Func<TService> instanceCreator, 
            Container container)
            where TService : class;

        public Registration CreateRegistration(Type serviceType, Type implementationType,
            Container container)
        {
            Requires.IsNotNull(serviceType, "serviceType");
            Requires.IsNotNull(implementationType, "implementationType");
            Requires.IsNotNull(container, "container");

            Requires.IsReferenceType(serviceType, "serviceType");
            Requires.IsReferenceType(implementationType, "implementationType");

            Requires.ServiceIsAssignableFromImplementation(serviceType, implementationType, 
                "implementationType");

            var closedCreateRegistrationMethod = OpenCreateRegistrationTServiceTImplementationMethod
                .MakeGenericMethod(serviceType, implementationType);

            try
            {
                return (Registration)
                    closedCreateRegistrationMethod.Invoke(this, new object[] { container });
            }
            catch (MemberAccessException ex)
            {
                // This happens when the user tries to resolve an internal type inside a (Silverlight) sandbox.
                throw new ArgumentException(
                    StringResources.UnableToResolveTypeDueToSecurityConfiguration(implementationType, ex),
#if !SILVERLIGHT
                    "implementationType", 
#endif
                    ex);
            }
        }

        public Registration CreateRegistration(Type serviceType, Func<object> instanceCreator,
            Container container)
        {
            Requires.IsNotNull(serviceType, "serviceType");
            Requires.IsNotNull(instanceCreator, "instanceCreator");
            Requires.IsNotNull(container, "container");

            Requires.IsReferenceType(serviceType, "serviceType");

            var closedCreateRegistrationMethod = OpenCreateRegistrationTServiceFuncMethod
                .MakeGenericMethod(serviceType);

            try
            {
                // Build the following delegate: () => (ServiceType)instanceCreator();
                var typeSafeInstanceCreator = ConvertDelegateToTypeSafeDelegate(serviceType, instanceCreator);
                
                return (Registration)closedCreateRegistrationMethod.Invoke(this, 
                    new object[] { typeSafeInstanceCreator, container });
            }
            catch (MemberAccessException ex)
            {
                // This happens when the user tries to resolve an internal type inside a (Silverlight) sandbox.
                throw new ArgumentException(
                    StringResources.UnableToResolveTypeDueToSecurityConfiguration(serviceType, ex),
#if !SILVERLIGHT
                    "serviceType",
#endif
                    ex);
            }
        }

        internal Registration CreateRegistration(Type serviceType, Type implementationType,
            Container container, IEnumerable<Tuple<ParameterInfo, Expression>> overriddenParameters)
        {
            var registration = this.CreateRegistration(serviceType, implementationType, container);

            registration.SetParameterOverrides(overriddenParameters);

            return registration;
        }

        private static object ConvertDelegateToTypeSafeDelegate(Type serviceType, Func<object> instanceCreator)
        {
            // Build the following delegate: () => (ServiceType)instanceCreator();
            var invocationExpression =
                Expression.Invoke(Expression.Constant(instanceCreator), new Expression[0]);

            var convertExpression = Expression.Convert(invocationExpression, serviceType);

            var parameters = new ParameterExpression[0];

            // This might throw an MemberAccessException when serviceType is internal while we're running in
            // a Silverlight sandbox.
            return Expression.Lambda(convertExpression, parameters).Compile();
        }

        private static MethodInfo GetMethod(Expression<Action<Lifestyle>> methodCall)
        {
            var body = methodCall.Body as MethodCallExpression;
            return body.Method.GetGenericMethodDefinition();
        }
    }
}