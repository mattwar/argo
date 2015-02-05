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
    public abstract class EncodingMember
    {
        public abstract string Name { get; }
        public abstract bool CanWrite { get; }
        public abstract Type Type { get; }
        public abstract object GetValue(object instance);
        public abstract void SetValue(object instance, object value);

        public static EncodingMember Create(MemberInfo member)
        {
            var prop = member as PropertyInfo;
            if (prop != null)
            {
                var typeArgs = new Type[] { prop.DeclaringType, prop.PropertyType };
                var getter = Delegate.CreateDelegate(typeof(GetAccessor<,>).MakeGenericType(typeArgs), prop.GetGetMethod());
                var setter = prop.CanWrite ? Delegate.CreateDelegate(typeof(SetAccessor<,>).MakeGenericType(typeArgs), prop.GetSetMethod()) : null;

                return (EncodingMember)Activator.CreateInstance(typeof(DelegatedEncodingMember<,>).MakeGenericType(typeArgs), new object[] { prop.Name, getter, setter });
            }

            var field = member as FieldInfo;
            if (field != null)
            {
                var instanceParam = Expression.Parameter(field.DeclaringType, "instance");
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
    }

    public abstract class EncodingMember<TInstance, TMember> : EncodingMember
    {
        public abstract TMember GetTypedValue(TInstance instance);
        public abstract void SetTypedValue(TInstance instance, TMember member);
    }

    public delegate TMember GetAccessor<TInstance, TMember>(TInstance instance);
    public delegate void SetAccessor<TInstance, TMember>(TInstance instance, TMember member);

    public class DelegatedEncodingMember<TInstance, TMember> : EncodingMember<TInstance, TMember>
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

        public override object GetValue(object instance)
        {
            return this.GetTypedValue((TInstance)instance);
        }

        public override void SetValue(object instance, object value)
        {
            this.SetTypedValue((TInstance)instance, (TMember)value);
        }

        public override TMember GetTypedValue(TInstance instance)
        {
            return this.getAccessor(instance);
        }

        public override void SetTypedValue(TInstance instance, TMember member)
        {
            this.setAccessor(instance, member);
        }
    }
}
