// STUB (integrator, CONTRACT P0.2(e)) — delete this file when the integrator's own copy lands.
//
// C# 9 positional records generate init-only setters, which the compiler emits a reference to
// System.Runtime.CompilerServices.IsExternalInit for. It is absent from this project's
// compilation context, so without this shim NO shared record type compiles at all.
namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
