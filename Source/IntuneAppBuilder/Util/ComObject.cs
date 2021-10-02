using System;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace IntuneAppBuilder.Util
{
    /// <summary>
    ///     dotnet core doesn't support COM using dynamic.
    /// </summary>
    internal class ComObject : DynamicObject, IDisposable
    {
        private readonly object instance;

        public ComObject(object instance) => this.instance = instance;

        public void Dispose() => Marshal.FinalReleaseComObject(instance);

        [DebuggerNonUserCode]
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            try
            {
                result = Wrap(instance.GetType().InvokeMember(
                    binder.Name,
                    BindingFlags.GetProperty,
                    Type.DefaultBinder,
                    instance,
                    new object[] { }
                ));
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                result = null;
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }

            return true;
        }

        [DebuggerNonUserCode]
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            var name = binder.Name;
            var flags = BindingFlags.InvokeMethod;
            if (name.StartsWith("get_") || name.StartsWith("set_"))
            {
                flags = name.StartsWith("get_") ? BindingFlags.GetProperty : BindingFlags.SetProperty;
                name = name.Substring(4);
            }

            try
            {
                result = Wrap(instance.GetType().InvokeMember(
                    name,
                    flags,
                    Type.DefaultBinder,
                    instance,
                    args.Select(Unwrap).ToArray()));
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                result = null;
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }

            return true;
        }

        [DebuggerNonUserCode]
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            try
            {
                instance.GetType()
                    .InvokeMember(
                        binder.Name,
                        BindingFlags.SetProperty,
                        Type.DefaultBinder,
                        instance,
                        new[]
                        {
                            Unwrap(value)
                        }
                    );
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }

            return true;
        }

        private object Unwrap(object value) =>
            value is ComObject comObject
                ? comObject.instance
                : value;

        private object Wrap(object value) => value is MarshalByRefObject ? new ComObject(value) : value;

        public static ComObject CreateObject(string progId) => new ComObject(Activator.CreateInstance(Type.GetTypeFromProgID(progId, true)));
    }
}