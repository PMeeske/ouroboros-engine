namespace Ouroboros.Pipeline.Hyperon;

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

/// <summary>
/// P/Invoke bindings for the hyperonc C library (MeTTa symbolic reasoning engine).
/// Source: hyperon-experimental/c/src/lib.rs, atom.rs, space.rs, metta.rs
/// </summary>
internal static partial class HyperonNative
{
    private const string DllName = "hyperonc";

    // ── Atom Type Enum ──────────────────────────────────────────────────────

    public enum AtomType
    {
        Symbol = 0,
        Variable = 1,
        Expression = 2,
        Grounded = 3,
    }

    // ── SafeHandle wrappers ─────────────────────────────────────────────────

    public sealed class AtomHandle : SafeHandle
    {
        public AtomHandle() : base(IntPtr.Zero, ownsHandle: true) { }
        public override bool IsInvalid => handle == IntPtr.Zero;
        protected override bool ReleaseHandle()
        {
            if (handle != IntPtr.Zero) atom_free(handle);
            handle = IntPtr.Zero;
            return true;
        }
    }

    public sealed class SpaceHandle : SafeHandle
    {
        public SpaceHandle() : base(IntPtr.Zero, ownsHandle: true) { }
        public override bool IsInvalid => handle == IntPtr.Zero;
        protected override bool ReleaseHandle()
        {
            if (handle != IntPtr.Zero) space_free(handle);
            handle = IntPtr.Zero;
            return true;
        }
    }

    public sealed class MettaHandle : SafeHandle
    {
        public MettaHandle() : base(IntPtr.Zero, ownsHandle: true) { }
        public override bool IsInvalid => handle == IntPtr.Zero;
        protected override bool ReleaseHandle()
        {
            if (handle != IntPtr.Zero) metta_free(handle);
            handle = IntPtr.Zero;
            return true;
        }
    }

    public sealed class BindingsHandle : SafeHandle
    {
        public BindingsHandle() : base(IntPtr.Zero, ownsHandle: true) { }
        public override bool IsInvalid => handle == IntPtr.Zero;
        protected override bool ReleaseHandle()
        {
            if (handle != IntPtr.Zero) bindings_free(handle);
            handle = IntPtr.Zero;
            return true;
        }
    }

    public sealed class BindingsSetHandle : SafeHandle
    {
        public BindingsSetHandle() : base(IntPtr.Zero, ownsHandle: true) { }
        public override bool IsInvalid => handle == IntPtr.Zero;
        protected override bool ReleaseHandle()
        {
            if (handle != IntPtr.Zero) bindings_set_free(handle);
            handle = IntPtr.Zero;
            return true;
        }
    }

    public sealed class SExprParserHandle : SafeHandle
    {
        public SExprParserHandle() : base(IntPtr.Zero, ownsHandle: true) { }
        public override bool IsInvalid => handle == IntPtr.Zero;
        protected override bool ReleaseHandle()
        {
            if (handle != IntPtr.Zero) sexpr_parser_free(handle);
            handle = IntPtr.Zero;
            return true;
        }
    }

    // ── Callback delegates ───────────────────────────────────────────────────

    /// <summary>Callback receiving an atom reference and user context.</summary>
    public delegate void AtomCallback(IntPtr atom, IntPtr context);

    /// <summary>Callback receiving an atom vector (ptr + count) and user context.</summary>
    public delegate void AtomVecCallback(IntPtr atoms, UIntPtr count, IntPtr context);

    /// <summary>Callback receiving a mutable bindings and user context.</summary>
    public delegate void BindingsMutCallback(IntPtr bindings, IntPtr context);

    // ── Atom FFI ─────────────────────────────────────────────────────────────

    [LibraryImport(DllName)]
    public static partial IntPtr atom_sym([MarshalAs(UnmanagedType.LPStr)] string name);

    [LibraryImport(DllName)]
    public static partial IntPtr atom_var([MarshalAs(UnmanagedType.LPStr)] string name);

    [LibraryImport(DllName)]
    public static partial IntPtr atom_expr(IntPtr children, UIntPtr size);

    [LibraryImport(DllName)]
    public static partial void atom_free(IntPtr atom);

    [LibraryImport(DllName)]
    public static partial IntPtr atom_clone(IntPtr atom);

    [LibraryImport(DllName)]
    public static partial UIntPtr atom_to_str(IntPtr atom, IntPtr buf, UIntPtr bufLen);

    [LibraryImport(DllName)]
    public static partial UIntPtr atom_get_name(IntPtr atom, IntPtr buf, UIntPtr bufLen);

    [LibraryImport(DllName)]
    public static partial AtomType atom_get_metatype(IntPtr atom);

    [LibraryImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool atom_eq(IntPtr a, IntPtr b);

    [LibraryImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool atom_is_null(IntPtr atom);

    [LibraryImport(DllName)]
    public static partial void atom_get_children(IntPtr atom, AtomVecCallback callback, IntPtr context);

    // ── Space FFI ────────────────────────────────────────────────────────────

    [LibraryImport(DllName)]
    public static partial IntPtr space_new_grounding_space();

    [LibraryImport(DllName)]
    public static partial void space_free(IntPtr space);

    [LibraryImport(DllName)]
    public static partial void space_add(IntPtr space, IntPtr atom);

    [LibraryImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool space_remove(IntPtr space, IntPtr atom);

    [LibraryImport(DllName)]
    public static partial IntPtr space_query(IntPtr space, IntPtr pattern);

    [LibraryImport(DllName)]
    public static partial IntPtr space_clone_handle(IntPtr space);

    [LibraryImport(DllName)]
    public static partial nint space_atom_count(IntPtr space);

    [LibraryImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool space_iterate(IntPtr space, AtomCallback callback, IntPtr context);

    // ── MeTTa Runner FFI ─────────────────────────────────────────────────────

    [LibraryImport(DllName)]
    public static partial IntPtr metta_new();

    [LibraryImport(DllName)]
    public static partial void metta_free(IntPtr metta);

    [LibraryImport(DllName)]
    public static partial void metta_run(IntPtr metta, IntPtr parser, AtomVecCallback callback, IntPtr context);

    [LibraryImport(DllName)]
    public static partial IntPtr metta_space(IntPtr metta);

    [LibraryImport(DllName)]
    public static partial void metta_evaluate_atom(IntPtr metta, IntPtr atom, AtomVecCallback callback, IntPtr context);

    [LibraryImport(DllName)]
    public static partial IntPtr metta_err_str(IntPtr metta);

    // ── S-Expression Parser FFI ──────────────────────────────────────────────

    [LibraryImport(DllName)]
    public static partial IntPtr sexpr_parser_new([MarshalAs(UnmanagedType.LPStr)] string text);

    [LibraryImport(DllName)]
    public static partial void sexpr_parser_free(IntPtr parser);

    [LibraryImport(DllName)]
    public static partial IntPtr sexpr_parser_parse(IntPtr parser, IntPtr tokenizer);

    // ── Bindings FFI ────────────────────────────────────────────────────────

    [LibraryImport(DllName)]
    public static partial IntPtr bindings_new();

    [LibraryImport(DllName)]
    public static partial void bindings_free(IntPtr bindings);

    [LibraryImport(DllName)]
    public static partial IntPtr bindings_clone(IntPtr bindings);

    [LibraryImport(DllName)]
    public static partial IntPtr bindings_resolve(IntPtr bindings, IntPtr var);

    [LibraryImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool bindings_add_var_binding(IntPtr bindings, IntPtr var, IntPtr atom);

    [LibraryImport(DllName)]
    public static partial void bindings_traverse(IntPtr bindings, IntPtr callback, IntPtr context);

    // ── Bindings Set FFI ────────────────────────────────────────────────────

    [LibraryImport(DllName)]
    public static partial IntPtr bindings_set_empty();

    [LibraryImport(DllName)]
    public static partial IntPtr bindings_set_single();

    [LibraryImport(DllName)]
    public static partial void bindings_set_free(IntPtr set);

    [LibraryImport(DllName)]
    public static partial IntPtr bindings_set_clone(IntPtr set);

    [LibraryImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool bindings_set_is_empty(IntPtr set);

    [LibraryImport(DllName)]
    public static partial void bindings_set_iterate(IntPtr set, BindingsMutCallback callback, IntPtr context);

    // ── Interpreter FFI ──────────────────────────────────────────────────────

    [LibraryImport(DllName)]
    public static partial IntPtr interpret_init(IntPtr space, IntPtr expr);

    [LibraryImport(DllName)]
    public static partial IntPtr interpret_step(IntPtr step);

    [LibraryImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool step_has_next(IntPtr step);

    [LibraryImport(DllName)]
    public static partial void step_get_result(IntPtr step, AtomVecCallback callback, IntPtr context);

    // ── Utility helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Reads a string from a hyperonc buffer-returning function (atom_to_str, atom_get_name, etc.).
    /// These functions return the required buffer size; call twice if needed.
    /// </summary>
    public static string ReadBufferString(Func<IntPtr, UIntPtr, UIntPtr> nativeFunc)
    {
        // First call with null buffer to get required size
        UIntPtr needed = nativeFunc(IntPtr.Zero, UIntPtr.Zero);
        if (needed == UIntPtr.Zero) return string.Empty;

        // Allocate buffer including null terminator
        int byteCount = (int)needed + 1;
        IntPtr buf = Marshal.AllocHGlobal(byteCount);
        try
        {
            nativeFunc(buf, (UIntPtr)byteCount);
            return Marshal.PtrToStringUTF8(buf)!;
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }
}