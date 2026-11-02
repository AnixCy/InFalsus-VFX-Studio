using System;
using System.Linq.Expressions;
using System.Reflection;
namespace InFalsusStudio.Performance {
 internal static class CompatibleFieldWrites {
  internal static Action<object,object> Create(FieldInfo field){
   // Legacy input hooks temporarily replace readonly collection references.
   // Expression.Assign rejects init-only fields; retain FieldInfo semantics.
   if(field.IsInitOnly||field.IsLiteral)return (target,value)=>field.SetValue(target,value);
   var targetArg=Expression.Parameter(typeof(object));var valueArg=Expression.Parameter(typeof(object));
   var target=field.IsStatic?null:Expression.Convert(targetArg,field.DeclaringType);
   var assign=Expression.Assign(Expression.Field(target,field),Expression.Convert(valueArg,field.FieldType));
   var write=Expression.Lambda<Action<object,object>>(assign,targetArg,valueArg).Compile();
   return (instance,value)=>{
    // The reflection binder also accepts numeric widening and null -> default.
    // A direct unbox/cast is equivalent only for assignable runtime values.
    if(value==null?!field.FieldType.IsValueType:field.FieldType.IsInstanceOfType(value))write(instance,value);
    else field.SetValue(instance,value);
   };
  }
 }
}
