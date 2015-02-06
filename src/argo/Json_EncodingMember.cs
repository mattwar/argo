using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Argo
{
    public static partial class Json
    {
        private abstract class EncodingMember
        {
            public abstract string Name { get; }
            public abstract bool CanWrite { get; }
            public abstract Type Type { get; }

            private static readonly ConcurrentDictionary<Type, IReadOnlyList<EncodingMember>> serializableMembers
                = new ConcurrentDictionary<Type, IReadOnlyList<EncodingMember>>();

            public static IReadOnlyList<EncodingMember> GetEncodingMembers(Type type)
            {
                IReadOnlyList<EncodingMember> members;
                if (!serializableMembers.TryGetValue(type, out members))
                {
                    IReadOnlyList<EncodingMember> tmp = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                                                        .Where(m => m.MemberType == MemberTypes.Field || m.MemberType == MemberTypes.Property)
                                                        .Select(m => Create(m)).ToList().AsReadOnly();

                    members = serializableMembers.GetOrAdd(type, tmp);
                }

                return members;
            }

            public static EncodingMember Create(MemberInfo member)
            {
                var prop = member as PropertyInfo;
                if (prop != null)
                {
                    var instanceParam = Expression.Parameter(prop.DeclaringType.MakeByRefType(), "instance");
                    var valueParam = Expression.Parameter(prop.PropertyType, "value");

                    var argTypes = new Type[] { prop.DeclaringType, prop.PropertyType };
                    var getterType = typeof(GetAccessor<,>).MakeGenericType(argTypes);
                    var getter = Expression.Lambda(getterType, Expression.Property(instanceParam, prop), instanceParam).Compile();
                    var setterType = typeof(SetAccessor<,>).MakeGenericType(argTypes);
                    var setter = Expression.Lambda(setterType, Expression.Assign(Expression.Property(instanceParam, prop), valueParam), instanceParam, valueParam).Compile();

                    return (EncodingMember)Activator.CreateInstance(typeof(DelegatedEncodingMember<,>).MakeGenericType(argTypes), new object[] { prop.Name, getter, setter });
                }

                var field = member as FieldInfo;
                if (field != null)
                {
                    var instanceParam = Expression.Parameter(field.DeclaringType.MakeByRefType(), "instance");
                    var valueParam = Expression.Parameter(field.FieldType, "value");

                    var argTypes = new Type[] { field.DeclaringType, field.FieldType };
                    var getterType = typeof(GetAccessor<,>).MakeGenericType(argTypes);
                    var getter = Expression.Lambda(getterType, Expression.Field(instanceParam, field), instanceParam).Compile();
                    var setterType = typeof(SetAccessor<,>).MakeGenericType(argTypes);
                    var setter = Expression.Lambda(setterType, Expression.Assign(Expression.Field(instanceParam, field), valueParam), instanceParam, valueParam).Compile();

                    return (EncodingMember)Activator.CreateInstance(typeof(DelegatedEncodingMember<,>).MakeGenericType(argTypes), new object[] { field.Name, getter, setter });
                }

                throw new ArgumentException("member must be field or property.", "member");
            }

            private delegate TMember GetAccessor<TInstance, TMember>(ref TInstance instance);
            private delegate void SetAccessor<TInstance, TMember>(ref TInstance instance, TMember member);

            private class DelegatedEncodingMember<TInstance, TMember> : EncodingMember<TInstance, TMember>
            {
                private readonly string name;
                private readonly GetAccessor<TInstance, TMember> getAccessor;
                private readonly SetAccessor<TInstance, TMember> setAccessor;

                public DelegatedEncodingMember(
                    string name,
                    GetAccessor<TInstance, TMember> getAccessor,
                    SetAccessor<TInstance, TMember> setAccessor)
                {
                    this.name = name;
                    this.getAccessor = getAccessor;
                    this.setAccessor = setAccessor;
                }

                public override string Name
                {
                    get { return this.name; }
                }

                public override bool CanWrite
                {
                    get { return this.setAccessor != null; }
                }

                public override Type Type
                {
                    get { return typeof(TMember); }
                }

                public override TMember GetTypedValue(ref TInstance instance)
                {
                    return this.getAccessor(ref instance);
                }

                public override void SetTypedValue(ref TInstance instance, TMember member)
                {
                    this.setAccessor(ref instance, member);
                }
            }
        }

        private abstract class EncodingMember<TInstance, TMember> : EncodingMember
        {
            public abstract TMember GetTypedValue(ref TInstance instance);
            public abstract void SetTypedValue(ref TInstance instance, TMember member);
        }
    }
}
