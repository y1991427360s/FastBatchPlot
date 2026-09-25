using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZwSoft.ZwCAD.Runtime;

namespace ZwSoft.ZwCAD.PlottingServices;

[Wrapper("AcPlPlotEngine")]
public class PlotEngine : DisposableWrapper
{
	public unsafe bool IsBackgroundPackaging
	{
		[return: MarshalAs(UnmanagedType.U1)]
		get
		{
			//IL_0012: Expected I, but got I8
			ZcPlPlotEngine* impObj = GetImpObj();
			return ((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, byte>)(*(ulong*)(*(long*)impObj + 80)))((nint)impObj) != 0;
		}
	}

	internal unsafe ZcPlPlotEngine* GetImpObj()
	{
		return (ZcPlPlotEngine*)base.UnmanagedObject.ToPointer();
	}

	internal PlotEngine(IntPtr unmanagedPointer, [MarshalAs(UnmanagedType.U1)] bool autoDelete)
		: base(unmanagedPointer, autoDelete)
	{
	}

	public unsafe static PlotEngine CopyFromUnmanagedObject(IntPtr unmanagedPointer)
	{
		//IL_001a: Expected I, but got I8
		ZcPlPlotEngine* ptr = (ZcPlPlotEngine*)global::<Module>.@new(8uL);
		ZcPlPlotEngine* value;
		try
		{
			if (ptr != null)
			{
				unmanagedPointer.ToPointer();
				value = ptr;
			}
			else
			{
				value = null;
			}
		}
		catch
		{
			//try-fault
			global::<Module>.delete(ptr, 8uL);
			throw;
		}
		IntPtr unmanagedPointer2 = new IntPtr(value);
		return new PlotEngine(unmanagedPointer2, autoDelete: true);
	}

	public unsafe void BeginPlot(PlotProgress plotProgress, object parameters)
	{
		//IL_0003: Expected I, but got I8
		//IL_0026: Expected I, but got I8
		//IL_0026: Expected I, but got I8
		ZcPlPlotProgress* ptr = null;
		if (null != plotProgress)
		{
			ptr = plotProgress.GetImpObj();
		}
		ZcPlPlotEngine* impObj = GetImpObj();
		Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcPlPlotProgress*, void*, global::Zcad.ErrorStatus>)(*(ulong*)(*(ulong*)impObj)))((nint)impObj, ptr, null));
	}

	public unsafe void EndPlot(object parameters)
	{
		//IL_0015: Expected I, but got I8
		//IL_0015: Expected I, but got I8
		ZcPlPlotEngine* impObj = GetImpObj();
		Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, void*, global::Zcad.ErrorStatus>)(*(ulong*)(*(long*)impObj + 8)))((nint)impObj, null));
	}

	public unsafe void BeginDocument(PlotInfo plotInfo, string documentName, object parameters, int copies, [MarshalAs(UnmanagedType.U1)] bool plotToFile, string fileName)
	{
		//IL_0021: Expected I, but got I8
		//IL_0025: Expected I, but got I8
		//IL_0041: Expected I, but got I8
		//IL_0041: Expected I, but got I8
		//IL_0062: Expected I, but got I8
		//IL_0094: Expected I, but got I8
		System.Runtime.CompilerServices.Unsafe.SkipInit(out global::StringToWchar stringToWchar);
		global::StringToWchar* ptr = global::<Module>.StringToWchar.{ctor}(&stringToWchar, fileName);
		try
		{
			System.Runtime.CompilerServices.Unsafe.SkipInit(out global::StringToWchar stringToWchar2);
			global::StringToWchar* ptr2 = global::<Module>.StringToWchar.{ctor}(&stringToWchar2, documentName);
			try
			{
				ZcPlPlotEngine* impObj = GetImpObj();
				char* ptr3 = (char*)(*(ulong*)ptr);
				char* ptr4 = (char*)(*(ulong*)ptr2);
				Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcPlPlotInfo*, char*, void*, int, byte, char*, global::Zcad.ErrorStatus>)(*(ulong*)(*(long*)impObj + 16)))((nint)impObj, plotInfo.GetImpObj(), ptr4, null, copies, plotToFile ? ((byte)1) : ((byte)0), ptr3));
			}
			catch
			{
				//try-fault
				global::<Module>.___CxxCallUnwindDtor((delegate*<void*, void>)(delegate*<global::StringToWchar*, void>)(&global::<Module>.StringToWchar.{dtor}), &stringToWchar2);
				throw;
			}
			IntPtr intPtr = new IntPtr((void*)System.Runtime.CompilerServices.Unsafe.As<global::StringToWchar, ulong>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref stringToWchar2, 8)));
			((GCHandle)intPtr).Free();
			System.Runtime.CompilerServices.Unsafe.As<global::StringToWchar, long>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref stringToWchar2, 8)) = 0L;
		}
		catch
		{
			//try-fault
			global::<Module>.___CxxCallUnwindDtor((delegate*<void*, void>)(delegate*<global::StringToWchar*, void>)(&global::<Module>.StringToWchar.{dtor}), &stringToWchar);
			throw;
		}
		IntPtr intPtr2 = new IntPtr((void*)System.Runtime.CompilerServices.Unsafe.As<global::StringToWchar, ulong>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref stringToWchar, 8)));
		((GCHandle)intPtr2).Free();
	}

	public unsafe void EndDocument(object parameters)
	{
		//IL_0016: Expected I, but got I8
		//IL_0016: Expected I, but got I8
		ZcPlPlotEngine* impObj = GetImpObj();
		Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, void*, global::Zcad.ErrorStatus>)(*(ulong*)(*(long*)impObj + 24)))((nint)impObj, null));
	}

	public unsafe void BeginPage(PlotPageInfo pageInfo, PlotInfo plotInfo, [MarshalAs(UnmanagedType.U1)] bool lastPage, object parameters)
	{
		//IL_0023: Expected I, but got I8
		//IL_0023: Expected I, but got I8
		ZcPlPlotEngine* impObj = GetImpObj();
		Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcPlPlotPageInfo*, ZcPlPlotInfo*, byte, void*, global::Zcad.ErrorStatus>)(*(ulong*)(*(long*)impObj + 32)))((nint)impObj, pageInfo.GetImpObj(), plotInfo.GetImpObj(), lastPage ? ((byte)1) : ((byte)0), null));
	}

	public unsafe void EndPage(object parameters)
	{
		//IL_0047: Expected I, but got I8
		//IL_0047: Expected I, but got I8
		//IL_002a: Expected I, but got I8
		PreviewEndPlotInfo previewEndPlotInfo = parameters as PreviewEndPlotInfo;
		if (previewEndPlotInfo != null)
		{
			ZcPlPlotEngine* impObj = GetImpObj();
			Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, void*, global::Zcad.ErrorStatus>)(*(ulong*)(*(long*)impObj + 40)))((nint)impObj, previewEndPlotInfo.GetImpObj()));
		}
		else
		{
			ZcPlPlotEngine* impObj2 = GetImpObj();
			Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, void*, global::Zcad.ErrorStatus>)(*(ulong*)(*(long*)impObj2 + 40)))((nint)impObj2, null));
		}
	}

	public unsafe void BeginGenerateGraphics(object parameters)
	{
		//IL_0016: Expected I, but got I8
		//IL_0016: Expected I, but got I8
		ZcPlPlotEngine* impObj = GetImpObj();
		Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, void*, global::Zcad.ErrorStatus>)(*(ulong*)(*(long*)impObj + 56)))((nint)impObj, null));
	}

	public unsafe void EndGenerateGraphics(object parameters)
	{
		//IL_0016: Expected I, but got I8
		//IL_0016: Expected I, but got I8
		ZcPlPlotEngine* impObj = GetImpObj();
		Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, void*, global::Zcad.ErrorStatus>)(*(ulong*)(*(long*)impObj + 64)))((nint)impObj, null));
	}

	public void Destroy()
	{
		((IDisposable)this)?.Dispose();
	}

	protected unsafe sealed override void DeleteUnmanagedObject()
	{
		//IL_0012: Expected I, but got I8
		ZcPlPlotEngine* impObj = GetImpObj();
		((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, void>)(*(ulong*)(*(long*)impObj + 72)))((nint)impObj);
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.0.0.9375' (yours is '8.2.0.7535-95108c96')
