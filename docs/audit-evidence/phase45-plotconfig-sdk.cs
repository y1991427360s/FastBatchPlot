using System;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZwSoft.ZwCAD.Geometry;
using ZwSoft.ZwCAD.Runtime;

namespace ZwSoft.ZwCAD.PlottingServices;

[Wrapper("AcPlPlotConfig")]
public sealed class PlotConfig : RXObject
{
	public unsafe PlotToFileCapability PlotToFileCapability
	{
		get
		{
			//IL_0015: Expected I, but got I8
			ZcPlPlotConfig* impObj = GetImpObj();
			return (PlotToFileCapability)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcPlPlotConfig.PlotToFileCapability>)(*(ulong*)(*(long*)impObj + 152)))((nint)impObj);
		}
	}

	public unsafe string DefaultFileExtension
	{
		get
		{
			//IL_0019: Expected I, but got I8
			ZcPlPlotConfig* impObj = GetImpObj();
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* value);
			((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, char**, global::Zcad.ErrorStatus>)(*(ulong*)(*(long*)impObj + 144)))((nint)impObj, &value);
			return new string(value);
		}
	}

	public unsafe bool IsPlotToFile
	{
		[return: MarshalAs(UnmanagedType.U1)]
		get
		{
			//IL_0015: Expected I, but got I8
			ZcPlPlotConfig* impObj = GetImpObj();
			return ((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, byte>)(*(ulong*)(*(long*)impObj + 128)))((nint)impObj) != 0;
		}
		[param: MarshalAs(UnmanagedType.U1)]
		set
		{
			//IL_0018: Expected I, but got I8
			ZcPlPlotConfig* impObj = GetImpObj();
			Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, byte, global::Zcad.ErrorStatus>)(*(ulong*)(*(long*)impObj + 136)))((nint)impObj, value ? ((byte)1) : ((byte)0)));
		}
	}

	public unsafe StringCollection CanonicalMediaNames
	{
		get
		{
			//IL_002f: Expected I, but got I8
			//IL_004c: Expected I, but got I8
			//IL_0055: Expected I, but got I8
			//IL_0069: Expected I, but got I8
			System.Runtime.CompilerServices.Unsafe.SkipInit(out global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> > obj);
			*(long*)(&obj) = 0L;
			System.Runtime.CompilerServices.Unsafe.As<global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 8)) = 0;
			System.Runtime.CompilerServices.Unsafe.As<global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 12)) = 0;
			System.Runtime.CompilerServices.Unsafe.As<global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 16)) = 8;
			StringCollection stringCollection;
			try
			{
				ZcPlPlotConfig* impObj = GetImpObj();
				((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> >*, void>)(*(ulong*)(*(long*)impObj + 96)))((nint)impObj, &obj);
				stringCollection = new StringCollection();
				int num = 0;
				if (0 < System.Runtime.CompilerServices.Unsafe.As<global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 12)))
				{
					long num2 = 0L;
					do
					{
						char** ptr = (char**)(num2 + *(long*)(&obj));
						stringCollection.Add(new string((char*)(*(ulong*)ptr)));
						global::<Module>.zcutDelBuffer((void**)((long)num * 8L + *(long*)(&obj)));
						num++;
						num2 += 8;
					}
					while (num < System.Runtime.CompilerServices.Unsafe.As<global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 12)));
				}
			}
			catch
			{
				//try-fault
				global::<Module>.___CxxCallUnwindDtor((delegate*<void*, void>)(delegate*<global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> >*, void>)(&global::<Module>.ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> >.{dtor}), &obj);
				throw;
			}
			if (System.Runtime.CompilerServices.Unsafe.As<global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 8)) > 0)
			{
				global::<Module>.ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> >.setPhysicalLength(&obj, 0);
			}
			return stringCollection;
		}
	}

	public unsafe int MaximumDeviceDotsPerInch
	{
		get
		{
			//IL_0012: Expected I, but got I8
			ZcPlPlotConfig* impObj = GetImpObj();
			return (int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, uint>)(*(ulong*)(*(long*)impObj + 80)))((nint)impObj);
		}
	}

	public unsafe int DeviceType
	{
		get
		{
			//IL_0012: Expected I, but got I8
			ZcPlPlotConfig* impObj = GetImpObj();
			return (int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, uint>)(*(ulong*)(*(long*)impObj + 88)))((nint)impObj);
		}
	}

	public unsafe string FullPath
	{
		get
		{
			//IL_0012: Expected I, but got I8
			ZcPlPlotConfig* impObj = GetImpObj();
			return new string(((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, char*>)(*(ulong*)(*(long*)impObj + 72)))((nint)impObj));
		}
	}

	public unsafe string DeviceName
	{
		get
		{
			//IL_0012: Expected I, but got I8
			ZcPlPlotConfig* impObj = GetImpObj();
			return new string(((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, char*>)(*(ulong*)(*(long*)impObj + 64)))((nint)impObj));
		}
	}

	public unsafe string TagLine
	{
		get
		{
			//IL_0020: Expected I, but got I8
			ZcPlPlotConfig* impObj = GetImpObj();
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr2);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr3);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr4);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr5);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* value);
			((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, char**, char**, char**, char**, char**, char**, void>)(*(ulong*)(*(long*)impObj + 56)))((nint)impObj, &ptr, &ptr2, &ptr3, &ptr4, &ptr5, &value);
			string result = new string(value);
			global::<Module>.zcutDelBuffer((void**)(&ptr));
			global::<Module>.zcutDelBuffer((void**)(&ptr2));
			global::<Module>.zcutDelBuffer((void**)(&ptr3));
			global::<Module>.zcutDelBuffer((void**)(&ptr4));
			global::<Module>.zcutDelBuffer((void**)(&ptr5));
			global::<Module>.zcutDelBuffer((void**)(&value));
			return result;
		}
	}

	public unsafe string ServerName
	{
		get
		{
			//IL_0020: Expected I, but got I8
			ZcPlPlotConfig* impObj = GetImpObj();
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr2);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr3);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr4);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* value);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr5);
			((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, char**, char**, char**, char**, char**, char**, void>)(*(ulong*)(*(long*)impObj + 56)))((nint)impObj, &ptr, &ptr2, &ptr3, &ptr4, &value, &ptr5);
			string result = new string(value);
			global::<Module>.zcutDelBuffer((void**)(&ptr));
			global::<Module>.zcutDelBuffer((void**)(&ptr2));
			global::<Module>.zcutDelBuffer((void**)(&ptr3));
			global::<Module>.zcutDelBuffer((void**)(&ptr4));
			global::<Module>.zcutDelBuffer((void**)(&value));
			global::<Module>.zcutDelBuffer((void**)(&ptr5));
			return result;
		}
	}

	public unsafe string PortName
	{
		get
		{
			//IL_0020: Expected I, but got I8
			ZcPlPlotConfig* impObj = GetImpObj();
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr2);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr3);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* value);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr4);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr5);
			((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, char**, char**, char**, char**, char**, char**, void>)(*(ulong*)(*(long*)impObj + 56)))((nint)impObj, &ptr, &ptr2, &ptr3, &value, &ptr4, &ptr5);
			string result = new string(value);
			global::<Module>.zcutDelBuffer((void**)(&ptr));
			global::<Module>.zcutDelBuffer((void**)(&ptr2));
			global::<Module>.zcutDelBuffer((void**)(&ptr3));
			global::<Module>.zcutDelBuffer((void**)(&value));
			global::<Module>.zcutDelBuffer((void**)(&ptr4));
			global::<Module>.zcutDelBuffer((void**)(&ptr5));
			return result;
		}
	}

	public unsafe string Comment
	{
		get
		{
			//IL_0020: Expected I, but got I8
			ZcPlPlotConfig* impObj = GetImpObj();
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr2);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* value);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr3);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr4);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr5);
			((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, char**, char**, char**, char**, char**, char**, void>)(*(ulong*)(*(long*)impObj + 56)))((nint)impObj, &ptr, &ptr2, &value, &ptr3, &ptr4, &ptr5);
			string result = new string(value);
			global::<Module>.zcutDelBuffer((void**)(&ptr));
			global::<Module>.zcutDelBuffer((void**)(&ptr2));
			global::<Module>.zcutDelBuffer((void**)(&value));
			global::<Module>.zcutDelBuffer((void**)(&ptr3));
			global::<Module>.zcutDelBuffer((void**)(&ptr4));
			global::<Module>.zcutDelBuffer((void**)(&ptr5));
			return result;
		}
	}

	public unsafe string LocationName
	{
		get
		{
			//IL_0020: Expected I, but got I8
			ZcPlPlotConfig* impObj = GetImpObj();
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* value);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr2);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr3);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr4);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr5);
			((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, char**, char**, char**, char**, char**, char**, void>)(*(ulong*)(*(long*)impObj + 56)))((nint)impObj, &ptr, &value, &ptr2, &ptr3, &ptr4, &ptr5);
			string result = new string(value);
			global::<Module>.zcutDelBuffer((void**)(&ptr));
			global::<Module>.zcutDelBuffer((void**)(&value));
			global::<Module>.zcutDelBuffer((void**)(&ptr2));
			global::<Module>.zcutDelBuffer((void**)(&ptr3));
			global::<Module>.zcutDelBuffer((void**)(&ptr4));
			global::<Module>.zcutDelBuffer((void**)(&ptr5));
			return result;
		}
	}

	public unsafe string DriverName
	{
		get
		{
			//IL_0020: Expected I, but got I8
			ZcPlPlotConfig* impObj = GetImpObj();
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* value);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr2);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr3);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr4);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out char* ptr5);
			((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, char**, char**, char**, char**, char**, char**, void>)(*(ulong*)(*(long*)impObj + 56)))((nint)impObj, &value, &ptr, &ptr2, &ptr3, &ptr4, &ptr5);
			string result = new string(value);
			global::<Module>.zcutDelBuffer((void**)(&value));
			global::<Module>.zcutDelBuffer((void**)(&ptr));
			global::<Module>.zcutDelBuffer((void**)(&ptr2));
			global::<Module>.zcutDelBuffer((void**)(&ptr3));
			global::<Module>.zcutDelBuffer((void**)(&ptr4));
			global::<Module>.zcutDelBuffer((void**)(&ptr5));
			return result;
		}
	}

	internal unsafe ZcPlPlotConfig* GetImpObj()
	{
		return (ZcPlPlotConfig*)base.UnmanagedObject.ToPointer();
	}

	internal PlotConfig(IntPtr unmanagedPointer, [MarshalAs(UnmanagedType.U1)] bool autoDelete)
		: base(unmanagedPointer, autoDelete)
	{
	}

	public unsafe void CopyToUnmanagedObject(IntPtr unmanagedPointer)
	{
		//IL_001b: Expected I, but got I8
		void* ptr = unmanagedPointer.ToPointer();
		((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, global::ZcRxObject*, global::Zcad.ErrorStatus>)(*(ulong*)(*(long*)ptr + 24)))((nint)ptr, (global::ZcRxObject*)GetImpObj());
	}

	public unsafe static PlotConfig CopyFromUnmanagedObject(IntPtr unmanagedPointer)
	{
		//IL_000a: Expected I, but got I8
		//IL_0051: Expected I, but got I8
		void* ptr = global::<Module>.zcHeapAlloc(null, 16uL);
		ZcPlPlotConfig* pThis = (ZcPlPlotConfig*)ptr;
		ZcPlPlotConfig* value;
		try
		{
			if (ptr != null)
			{
				ZcPlPlotConfig* ptr2 = (ZcPlPlotConfig*)unmanagedPointer.ToPointer();
				try
				{
					*(long*)((ulong)(nint)ptr + 8uL) = *(long*)((ulong)(nint)ptr2 + 8uL);
				}
				catch
				{
					//try-fault
					global::<Module>.___CxxCallUnwindDtor((delegate*<void*, void>)(delegate*<global::ZcRxObject*, void>)(&global::<Module>.ZcRxObject.{dtor}), pThis);
					throw;
				}
				try
				{
					*(long*)ptr = (nint)System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_7ZcPlPlotConfig@@6B@);
				}
				catch
				{
					//try-fault
					global::<Module>.___CxxCallUnwindDtor((delegate*<void*, void>)(delegate*<ZcPlObject*, void>)(&global::<Module>.ZcPlObject.{dtor}), pThis);
					throw;
				}
				value = (ZcPlPlotConfig*)ptr;
			}
			else
			{
				value = null;
			}
		}
		catch
		{
			//try-fault
			global::<Module>.___CxxCallUnwindDelDtor((delegate*<void*, void>)(&global::<Module>.ZcHeapOperators.delete), (void*)pThis);
			throw;
		}
		IntPtr unmanagedPointer2 = new IntPtr(value);
		return new PlotConfig(unmanagedPointer2, autoDelete: true);
	}

	public unsafe void SaveToPC3(string name)
	{
		//IL_0013: Expected I, but got I8
		//IL_0024: Expected I, but got I8
		//IL_0045: Expected I, but got I8
		System.Runtime.CompilerServices.Unsafe.SkipInit(out global::StringToWchar stringToWchar);
		global::StringToWchar* ptr = global::<Module>.StringToWchar.{ctor}(&stringToWchar, name);
		try
		{
			ZcPlPlotConfig* impObj = GetImpObj();
			char* ptr2 = (char*)(*(ulong*)ptr);
			Interop.CheckBoolean(((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, char*, byte>)(*(ulong*)(*(long*)impObj + 160)))((nint)impObj, ptr2));
		}
		catch
		{
			//try-fault
			global::<Module>.___CxxCallUnwindDtor((delegate*<void*, void>)(delegate*<global::StringToWchar*, void>)(&global::<Module>.StringToWchar.{dtor}), &stringToWchar);
			throw;
		}
		IntPtr intPtr = new IntPtr((void*)System.Runtime.CompilerServices.Unsafe.As<global::StringToWchar, ulong>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref stringToWchar, 8)));
		((GCHandle)intPtr).Free();
	}

	public unsafe void RefreshMediaNameList()
	{
		//IL_0012: Expected I, but got I8
		ZcPlPlotConfig* impObj = GetImpObj();
		((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, void>)(*(ulong*)(*(long*)impObj + 120)))((nint)impObj);
	}

	public unsafe MediaBounds GetMediaBounds(string canonicalMediaName)
	{
		//IL_006c: Expected I, but got I8
		//IL_007f: Expected I, but got I8
		//IL_009b: Expected I, but got I8
		System.Runtime.CompilerServices.Unsafe.SkipInit(out global::ZcGeBoundBlock2d zcGeBoundBlock2d);
		global::<Module>.ZcGeBoundBlock2d.{ctor}(&zcGeBoundBlock2d);
		MediaBounds result;
		try
		{
			System.Runtime.CompilerServices.Unsafe.SkipInit(out global::ZcGePoint2d zcGePoint2d);
			*(double*)(&zcGePoint2d) = 0.0;
			System.Runtime.CompilerServices.Unsafe.As<global::ZcGePoint2d, double>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref zcGePoint2d, 8)) = 0.0;
			System.Runtime.CompilerServices.Unsafe.SkipInit(out global::ZcGePoint2d zcGePoint2d2);
			*(double*)(&zcGePoint2d2) = 0.0;
			System.Runtime.CompilerServices.Unsafe.As<global::ZcGePoint2d, double>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref zcGePoint2d2, 8)) = 0.0;
			System.Runtime.CompilerServices.Unsafe.SkipInit(out global::ZcGePoint2d zcGePoint2d3);
			*(double*)(&zcGePoint2d3) = 0.0;
			System.Runtime.CompilerServices.Unsafe.As<global::ZcGePoint2d, double>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref zcGePoint2d3, 8)) = 0.0;
			System.Runtime.CompilerServices.Unsafe.SkipInit(out global::StringToWchar stringToWchar);
			global::StringToWchar* ptr = global::<Module>.StringToWchar.{ctor}(&stringToWchar, canonicalMediaName);
			try
			{
				ZcPlPlotConfig* impObj = GetImpObj();
				char* ptr2 = (char*)(*(ulong*)ptr);
				((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, char*, global::ZcGePoint2d*, global::ZcGeBoundBlock2d*, void>)(*(ulong*)(*(long*)impObj + 112)))((nint)impObj, ptr2, &zcGePoint2d, &zcGeBoundBlock2d);
			}
			catch
			{
				//try-fault
				global::<Module>.___CxxCallUnwindDtor((delegate*<void*, void>)(delegate*<global::StringToWchar*, void>)(&global::<Module>.StringToWchar.{dtor}), &stringToWchar);
				throw;
			}
			IntPtr intPtr = new IntPtr((void*)System.Runtime.CompilerServices.Unsafe.As<global::StringToWchar, ulong>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref stringToWchar, 8)));
			((GCHandle)intPtr).Free();
			global::<Module>.ZcGeBoundBlock2d.getMinMaxPoints(&zcGeBoundBlock2d, &zcGePoint2d2, &zcGePoint2d3);
			Point2d upperRightPrintableArea = new Point2d(*(double*)(&zcGePoint2d3), System.Runtime.CompilerServices.Unsafe.As<global::ZcGePoint2d, double>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref zcGePoint2d3, 8)));
			Point2d lowerLeftPrintableArea = new Point2d(*(double*)(&zcGePoint2d2), System.Runtime.CompilerServices.Unsafe.As<global::ZcGePoint2d, double>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref zcGePoint2d2, 8)));
			Point2d pageSize = new Point2d(*(double*)(&zcGePoint2d), System.Runtime.CompilerServices.Unsafe.As<global::ZcGePoint2d, double>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref zcGePoint2d, 8)));
			MediaBounds mediaBounds = new MediaBounds(pageSize, lowerLeftPrintableArea, upperRightPrintableArea);
			result = mediaBounds;
		}
		catch
		{
			//try-fault
			global::<Module>.___CxxCallUnwindDtor((delegate*<void*, void>)(delegate*<global::ZcGeBoundBlock2d*, void>)(&global::<Module>.ZcGeBoundBlock2d.{dtor}), &zcGeBoundBlock2d);
			throw;
		}
		global::<Module>.ZcGeEntity2d.{dtor}((global::ZcGeEntity2d*)(&zcGeBoundBlock2d));
		return result;
	}

	public unsafe string GetLocalMediaName(string canonicalMediaName)
	{
		//IL_0015: Expected I, but got I8
		//IL_0025: Expected I, but got I8
		//IL_0041: Expected I, but got I8
		System.Runtime.CompilerServices.Unsafe.SkipInit(out global::StringToWchar stringToWchar);
		global::StringToWchar* ptr = global::<Module>.StringToWchar.{ctor}(&stringToWchar, canonicalMediaName);
		System.Runtime.CompilerServices.Unsafe.SkipInit(out char* value);
		try
		{
			ZcPlPlotConfig* impObj = GetImpObj();
			char* ptr2 = (char*)(*(ulong*)ptr);
			((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, char*, char**, void>)(*(ulong*)(*(long*)impObj + 104)))((nint)impObj, ptr2, &value);
		}
		catch
		{
			//try-fault
			global::<Module>.___CxxCallUnwindDtor((delegate*<void*, void>)(delegate*<global::StringToWchar*, void>)(&global::<Module>.StringToWchar.{dtor}), &stringToWchar);
			throw;
		}
		IntPtr intPtr = new IntPtr((void*)System.Runtime.CompilerServices.Unsafe.As<global::StringToWchar, ulong>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref stringToWchar, 8)));
		((GCHandle)intPtr).Free();
		string result = new string(value);
		global::<Module>.zcutDelBuffer((void**)(&value));
		return result;
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.0.0.9375' (yours is '8.2.0.7535-95108c96')
