using System;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZwSoft.ZwCAD.Geometry;
using ZwSoft.ZwCAD.Runtime;

namespace ZwSoft.ZwCAD.DatabaseServices;

[Wrapper("ZcDbPlotSettingsValidator")]
public sealed class PlotSettingsValidator : DisposableWrapper
{
	public unsafe static PlotSettingsValidator Current
	{
		get
		{
			//IL_001d: Expected I, but got I8
			ZcDbHostApplicationServices* unmanagedObject = HostApplicationServices.Current.GetUnmanagedObject();
			IntPtr unmanagedPointer = new IntPtr(((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettingsValidator*>)(*(ulong*)(*(long*)unmanagedObject + 576)))((nint)unmanagedObject));
			return new PlotSettingsValidator(unmanagedPointer, autoDelete: false);
		}
	}

	internal unsafe ZcDbPlotSettingsValidator* GetUnmanagedObject()
	{
		return (ZcDbPlotSettingsValidator*)base.UnmanagedObject.ToPointer();
	}

	internal PlotSettingsValidator(IntPtr unmanagedPointer, [MarshalAs(UnmanagedType.U1)] bool autoDelete)
		: base(unmanagedPointer, autoDelete)
	{
	}

	protected unsafe sealed override void DeleteUnmanagedObject()
	{
		//IL_0008: Expected I8, but got I
		//IL_0065: Expected I, but got I8
		//IL_003a: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
		try
		{
			global::<Module>.delete(GetUnmanagedObject(), 8uL);
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}

	public unsafe StringCollection GetCanonicalMediaNameList(PlotSettings plotSet)
	{
		//Discarded unreachable code: IL_00f9
		//IL_0009: Expected I8, but got I
		//IL_0099: Expected I, but got I8
		//IL_006c: Expected I, but got I8
		//IL_0041: Expected I, but got I8
		//IL_00be: Expected I, but got I8
		//IL_00c7: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> > obj);
		*(long*)(&obj) = 0L;
		System.Runtime.CompilerServices.Unsafe.As<ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 8)) = 0;
		System.Runtime.CompilerServices.Unsafe.As<ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 12)) = 0;
		System.Runtime.CompilerServices.Unsafe.As<ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 16)) = 8;
		StringCollection stringCollection;
		try
		{
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
				Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >*, Zcad.ErrorStatus>)(*(ulong*)(*(long*)unmanagedObject + 120)))((nint)unmanagedObject, plotSet.GetUnmanagedObject(), &obj));
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			stringCollection = new StringCollection();
			long num3 = System.Runtime.CompilerServices.Unsafe.As<ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 12));
			if (num3 > 0)
			{
				long num4 = 0L;
				do
				{
					char** ptr2 = (char**)(num4 * 8 + *(long*)(&obj));
					stringCollection.Add(new string((char*)(*(ulong*)ptr2)));
					num4++;
				}
				while (num4 < num3);
			}
		}
		catch
		{
			//try-fault
			global::<Module>.___CxxCallUnwindDtor((delegate*<void*, void>)(delegate*<ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >*, void>)(&global::<Module>.ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >.{dtor}), &obj);
			throw;
		}
		if (System.Runtime.CompilerServices.Unsafe.As<ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 8)) > 0)
		{
			global::<Module>.ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >.setPhysicalLength(&obj, 0);
		}
		return stringCollection;
	}

	public unsafe string GetLocaleMediaName(PlotSettings plotSet, string canonicalName)
	{
		//IL_0008: Expected I8, but got I
		//IL_0093: Expected I, but got I8
		//IL_0068: Expected I, but got I8
		//IL_0014: Expected I, but got I8
		//IL_0035: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr2);
		try
		{
			fixed (char* ptr = &global::<Module>.PtrToStringChars(canonicalName))
			{
				char* value = null;
				ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
				Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, char*, char**, Zcad.ErrorStatus>)(*(ulong*)(*(long*)unmanagedObject + 136)))((nint)unmanagedObject, plotSet.GetUnmanagedObject(), ptr, &value));
				return new string(value);
			}
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr2) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr2));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
		return null;
	}

	public unsafe string GetLocaleMediaName(PlotSettings plotSet, int index)
	{
		//IL_0008: Expected I8, but got I
		//IL_008a: Expected I, but got I8
		//IL_005f: Expected I, but got I8
		//IL_000c: Expected I, but got I8
		//IL_002c: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
		try
		{
			char* value = null;
			ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
			Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, int, char**, Zcad.ErrorStatus>)(*(ulong*)(*(long*)unmanagedObject + 128)))((nint)unmanagedObject, plotSet.GetUnmanagedObject(), index, &value));
			return new string(value);
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
		return null;
	}

	public unsafe StringCollection GetPlotDeviceList()
	{
		//Discarded unreachable code: IL_00f3
		//IL_0009: Expected I8, but got I
		//IL_0093: Expected I, but got I8
		//IL_0066: Expected I, but got I8
		//IL_003b: Expected I, but got I8
		//IL_00b8: Expected I, but got I8
		//IL_00c1: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> > obj);
		*(long*)(&obj) = 0L;
		System.Runtime.CompilerServices.Unsafe.As<ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 8)) = 0;
		System.Runtime.CompilerServices.Unsafe.As<ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 12)) = 0;
		System.Runtime.CompilerServices.Unsafe.As<ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 16)) = 8;
		StringCollection stringCollection;
		try
		{
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
				Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >*, Zcad.ErrorStatus>)(*(ulong*)(*(long*)unmanagedObject + 112)))((nint)unmanagedObject, &obj));
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			stringCollection = new StringCollection();
			long num3 = System.Runtime.CompilerServices.Unsafe.As<ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 12));
			if (num3 > 0)
			{
				long num4 = 0L;
				do
				{
					char** ptr2 = (char**)(num4 * 8 + *(long*)(&obj));
					stringCollection.Add(new string((char*)(*(ulong*)ptr2)));
					num4++;
				}
				while (num4 < num3);
			}
		}
		catch
		{
			//try-fault
			global::<Module>.___CxxCallUnwindDtor((delegate*<void*, void>)(delegate*<ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >*, void>)(&global::<Module>.ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >.{dtor}), &obj);
			throw;
		}
		if (System.Runtime.CompilerServices.Unsafe.As<ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 8)) > 0)
		{
			global::<Module>.ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >.setPhysicalLength(&obj, 0);
		}
		return stringCollection;
	}

	public unsafe StringCollection GetPlotStyleSheetList()
	{
		//Discarded unreachable code: IL_00f6
		//IL_0009: Expected I8, but got I
		//IL_0096: Expected I, but got I8
		//IL_0069: Expected I, but got I8
		//IL_003e: Expected I, but got I8
		//IL_00bb: Expected I, but got I8
		//IL_00c4: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> > obj);
		*(long*)(&obj) = 0L;
		System.Runtime.CompilerServices.Unsafe.As<ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 8)) = 0;
		System.Runtime.CompilerServices.Unsafe.As<ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 12)) = 0;
		System.Runtime.CompilerServices.Unsafe.As<ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 16)) = 8;
		StringCollection stringCollection;
		try
		{
			System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
			try
			{
				ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
				Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >*, Zcad.ErrorStatus>)(*(ulong*)(*(long*)unmanagedObject + 152)))((nint)unmanagedObject, &obj));
			}
			catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
			{
				uint num2 = 0u;
				global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
				try
				{
					try
					{
						throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
					}
					catch when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
						return (byte)num2 != 0;
					}).Invoke())
					{
					}
					if (num2 != 0)
					{
						throw;
					}
				}
				finally
				{
					global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
				}
			}
			stringCollection = new StringCollection();
			long num3 = System.Runtime.CompilerServices.Unsafe.As<ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 12));
			if (num3 > 0)
			{
				long num4 = 0L;
				do
				{
					char** ptr2 = (char**)(num4 * 8 + *(long*)(&obj));
					stringCollection.Add(new string((char*)(*(ulong*)ptr2)));
					num4++;
				}
				while (num4 < num3);
			}
		}
		catch
		{
			//try-fault
			global::<Module>.___CxxCallUnwindDtor((delegate*<void*, void>)(delegate*<ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >*, void>)(&global::<Module>.ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >.{dtor}), &obj);
			throw;
		}
		if (System.Runtime.CompilerServices.Unsafe.As<ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 8)) > 0)
		{
			global::<Module>.ZcArray<wchar_t const *,ZcArrayMemCopyReallocator<wchar_t const *> >.setPhysicalLength(&obj, 0);
		}
		return stringCollection;
	}

	public unsafe void RefreshLists(PlotSettings plotSet)
	{
		//IL_0008: Expected I8, but got I
		//IL_0075: Expected I, but got I8
		//IL_004a: Expected I, but got I8
		//IL_0025: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
		try
		{
			ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
			((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, void>)(*(ulong*)(*(long*)unmanagedObject + 160)))((nint)unmanagedObject, plotSet.GetUnmanagedObject());
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}

	public unsafe void SetCanonicalMediaName(PlotSettings plotSet, string mediaName)
	{
		//IL_0008: Expected I8, but got I
		//IL_0080: Expected I, but got I8
		//IL_0055: Expected I, but got I8
		//IL_002b: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr2);
		try
		{
			fixed (char* ptr = &global::<Module>.PtrToStringChars(mediaName))
			{
				ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
				Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, char*, Zcad.ErrorStatus>)(*(ulong*)(*(long*)unmanagedObject + 8)))((nint)unmanagedObject, plotSet.GetUnmanagedObject(), ptr));
			}
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr2) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr2));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}

	public unsafe void SetClosestMediaName(PlotSettings plotSet, double paperWidth, double paperHeight, PlotPaperUnit units, [MarshalAs(UnmanagedType.U1)] bool matchPrintableArea)
	{
		//IL_0008: Expected I8, but got I
		//IL_0080: Expected I, but got I8
		//IL_0055: Expected I, but got I8
		//IL_002b: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
		try
		{
			ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
			Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, double, double, ZcDbPlotSettings.PlotPaperUnits, byte, Zcad.ErrorStatus>)(*(ulong*)(*(long*)unmanagedObject + 144)))((nint)unmanagedObject, plotSet.GetUnmanagedObject(), paperWidth, paperHeight, (ZcDbPlotSettings.PlotPaperUnits)units, matchPrintableArea ? ((byte)1) : ((byte)0)));
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}

	public unsafe void SetCurrentStyleSheet(PlotSettings plotSet, string styleSheetName)
	{
		//IL_0008: Expected I8, but got I
		//IL_0081: Expected I, but got I8
		//IL_0056: Expected I, but got I8
		//IL_002c: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr2);
		try
		{
			fixed (char* ptr = &global::<Module>.PtrToStringChars(styleSheetName))
			{
				ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
				Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, char*, Zcad.ErrorStatus>)(*(ulong*)(*(long*)unmanagedObject + 88)))((nint)unmanagedObject, plotSet.GetUnmanagedObject(), ptr));
			}
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr2) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr2));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}

	public unsafe void SetCustomPrintScale(PlotSettings plotSet, CustomScale scale)
	{
		//IL_0008: Expected I8, but got I
		//IL_0085: Expected I, but got I8
		//IL_005a: Expected I, but got I8
		//IL_0030: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
		try
		{
			ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
			Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, double, double, Zcad.ErrorStatus>)(*(ulong*)(*(long*)unmanagedObject + 80)))((nint)unmanagedObject, plotSet.GetUnmanagedObject(), scale.m_numerator, scale.m_denominator));
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}

	public unsafe void SetDefaultPlotConfig(PlotSettings plotSet)
	{
		//IL_0008: Expected I8, but got I
		//IL_007a: Expected I, but got I8
		//IL_004f: Expected I, but got I8
		//IL_0025: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
		try
		{
			ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
			Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, Zcad.ErrorStatus>)(*(ulong*)(*(long*)unmanagedObject + 176)))((nint)unmanagedObject, plotSet.GetUnmanagedObject()));
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}

	public unsafe void SetPlotCentered(PlotSettings plotSet, [MarshalAs(UnmanagedType.U1)] bool isCentered)
	{
		//IL_0008: Expected I8, but got I
		//IL_0078: Expected I, but got I8
		//IL_004d: Expected I, but got I8
		//IL_0023: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
		try
		{
			ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
			Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, byte, Zcad.ErrorStatus>)(*(ulong*)(*(long*)unmanagedObject + 40)))((nint)unmanagedObject, plotSet.GetUnmanagedObject(), isCentered ? ((byte)1) : ((byte)0)));
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}

	public unsafe void SetPlotConfigurationName(PlotSettings plotSet, string plotDeviceName, string mediaName)
	{
		//IL_0008: Expected I8, but got I
		//IL_0087: Expected I, but got I8
		//IL_005c: Expected I, but got I8
		//IL_0032: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr3);
		try
		{
			fixed (char* ptr = &global::<Module>.PtrToStringChars(plotDeviceName))
			{
				fixed (char* ptr2 = &global::<Module>.PtrToStringChars(mediaName))
				{
					ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
					Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, char*, char*, Zcad.ErrorStatus>)(*(ulong*)(*(ulong*)unmanagedObject)))((nint)unmanagedObject, plotSet.GetUnmanagedObject(), ptr, ptr2));
				}
			}
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr3) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr3));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}

	public unsafe void SetPlotOrigin(PlotSettings plotSet, Point2d origin)
	{
		//IL_0008: Expected I8, but got I
		//IL_008d: Expected I, but got I8
		//IL_0062: Expected I, but got I8
		//IL_0038: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
		try
		{
			ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
			double y = origin.y;
			double x = origin.x;
			Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, double, double, Zcad.ErrorStatus>)(*(ulong*)(*(long*)unmanagedObject + 16)))((nint)unmanagedObject, plotSet.GetUnmanagedObject(), x, y));
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}

	public unsafe void SetPlotPaperUnits(PlotSettings plotSet, PlotPaperUnit units)
	{
		//IL_0008: Expected I8, but got I
		//IL_0078: Expected I, but got I8
		//IL_004d: Expected I, but got I8
		//IL_0023: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
		try
		{
			ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
			Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, ZcDbPlotSettings.PlotPaperUnits, Zcad.ErrorStatus>)(*(ulong*)(*(long*)unmanagedObject + 24)))((nint)unmanagedObject, plotSet.GetUnmanagedObject(), (ZcDbPlotSettings.PlotPaperUnits)units));
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}

	public unsafe void SetPlotRotation(PlotSettings plotSet, PlotRotation rotationType)
	{
		//IL_0008: Expected I8, but got I
		//IL_0078: Expected I, but got I8
		//IL_004d: Expected I, but got I8
		//IL_0023: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
		try
		{
			ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
			Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, ZcDbPlotSettings.PlotRotation, Zcad.ErrorStatus>)(*(ulong*)(*(long*)unmanagedObject + 32)))((nint)unmanagedObject, plotSet.GetUnmanagedObject(), (ZcDbPlotSettings.PlotRotation)rotationType));
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}

	public unsafe void SetPlotType(PlotSettings plotSet, PlotType plotAreaType)
	{
		//IL_0008: Expected I8, but got I
		//IL_0078: Expected I, but got I8
		//IL_004d: Expected I, but got I8
		//IL_0023: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
		try
		{
			ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
			Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, ZcDbPlotSettings.PlotType, Zcad.ErrorStatus>)(*(ulong*)(*(long*)unmanagedObject + 48)))((nint)unmanagedObject, plotSet.GetUnmanagedObject(), (ZcDbPlotSettings.PlotType)plotAreaType));
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}

	public unsafe void SetPlotViewName(PlotSettings plotSet, string viewName)
	{
		//IL_0008: Expected I8, but got I
		//IL_0081: Expected I, but got I8
		//IL_0056: Expected I, but got I8
		//IL_002c: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr2);
		try
		{
			fixed (char* ptr = &global::<Module>.PtrToStringChars(viewName))
			{
				ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
				Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, char*, Zcad.ErrorStatus>)(*(ulong*)(*(long*)unmanagedObject + 64)))((nint)unmanagedObject, plotSet.GetUnmanagedObject(), ptr));
			}
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr2) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr2));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}

	public unsafe void SetPlotWindowArea(PlotSettings plotSet, Extents2d windowArea)
	{
		//IL_0008: Expected I8, but got I
		//IL_00db: Expected I, but got I8
		//IL_00b0: Expected I, but got I8
		//IL_0086: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
		try
		{
			Point2d max = windowArea.m_max;
			Point2d max2 = windowArea.m_max;
			Point2d min = windowArea.m_min;
			Point2d min2 = windowArea.m_min;
			ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
			double y = max.y;
			double x = max2.x;
			double y2 = min.y;
			double x2 = min2.x;
			Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, double, double, double, double, Zcad.ErrorStatus>)(*(ulong*)(*(long*)unmanagedObject + 56)))((nint)unmanagedObject, plotSet.GetUnmanagedObject(), x2, y2, x, y));
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}

	public unsafe void SetStdScale(PlotSettings plotSet, double standardScale)
	{
		//IL_0008: Expected I8, but got I
		//IL_0078: Expected I, but got I8
		//IL_004d: Expected I, but got I8
		//IL_0023: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
		try
		{
			ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
			Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, double, Zcad.ErrorStatus>)(*(ulong*)(*(long*)unmanagedObject + 104)))((nint)unmanagedObject, plotSet.GetUnmanagedObject(), standardScale));
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}

	public unsafe void SetStdScaleType(PlotSettings plotSet, StdScaleType scaleType)
	{
		//IL_0008: Expected I8, but got I
		//IL_0078: Expected I, but got I8
		//IL_004d: Expected I, but got I8
		//IL_0023: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
		try
		{
			ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
			Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, ZcDbPlotSettings.StdScaleType, Zcad.ErrorStatus>)(*(ulong*)(*(long*)unmanagedObject + 96)))((nint)unmanagedObject, plotSet.GetUnmanagedObject(), (ZcDbPlotSettings.StdScaleType)scaleType));
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}

	public unsafe void SetUseStandardScale(PlotSettings plotSet, [MarshalAs(UnmanagedType.U1)] bool useStandard)
	{
		//IL_0008: Expected I8, but got I
		//IL_0078: Expected I, but got I8
		//IL_004d: Expected I, but got I8
		//IL_0023: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
		try
		{
			ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
			Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, byte, Zcad.ErrorStatus>)(*(ulong*)(*(long*)unmanagedObject + 72)))((nint)unmanagedObject, plotSet.GetUnmanagedObject(), useStandard ? ((byte)1) : ((byte)0)));
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}

	public unsafe void SetZoomToPaperOnUpdate(PlotSettings plotSet, [MarshalAs(UnmanagedType.U1)] bool doZoom)
	{
		//IL_0008: Expected I8, but got I
		//IL_0076: Expected I, but got I8
		//IL_004b: Expected I, but got I8
		//IL_0026: Expected I, but got I8
		long num = (nint)stackalloc byte[global::<Module>.__CxxQueryExceptionSize()];
		System.Runtime.CompilerServices.Unsafe.SkipInit(out Zcad.ErrorStatus* ptr);
		try
		{
			ZcDbPlotSettingsValidator* unmanagedObject = GetUnmanagedObject();
			((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcDbPlotSettings*, byte, void>)(*(ulong*)(*(long*)unmanagedObject + 168)))((nint)unmanagedObject, plotSet.GetUnmanagedObject(), doZoom ? ((byte)1) : ((byte)0));
		}
		catch when (global::<Module>.__CxxExceptionFilter((void*)Marshal.GetExceptionPointers(), System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_R0?AW4ErrorStatus@Zcad@@@8), 8, &ptr) != 0)
		{
			uint num2 = 0u;
			global::<Module>.__CxxRegisterExceptionObject((void*)Marshal.GetExceptionPointers(), (void*)num);
			try
			{
				try
				{
					throw new ZwSoft.ZwCAD.Runtime.Exception((ErrorStatus)(*ptr));
				}
				catch when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					num2 = (uint)global::<Module>.__CxxDetectRethrow((void*)Marshal.GetExceptionPointers());
					return (byte)num2 != 0;
				}).Invoke())
				{
				}
				if (num2 != 0)
				{
					throw;
				}
			}
			finally
			{
				global::<Module>.__CxxUnregisterExceptionObject((void*)num, (int)num2);
			}
		}
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.0.0.9375' (yours is '8.2.0.7535-95108c96')
