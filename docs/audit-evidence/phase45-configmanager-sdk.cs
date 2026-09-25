using System;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZwSoft.ZwCAD.Runtime;

namespace ZwSoft.ZwCAD.PlottingServices;

[Wrapper("AcPlPlotConfigManager")]
public sealed class PlotConfigManager
{
	public unsafe static PlotConfigInfoCollection Devices
	{
		get
		{
			//IL_002b: Expected I, but got I8
			//IL_004a: Expected I, but got I8
			ZcArray<ZcPlPlotConfigInfo,ZcArrayObjectCopyReallocator<ZcPlPlotConfigInfo> >* ptr = (ZcArray<ZcPlPlotConfigInfo,ZcArrayObjectCopyReallocator<ZcPlPlotConfigInfo> >*)global::<Module>.@new(24uL);
			ZcArray<ZcPlPlotConfigInfo,ZcArrayObjectCopyReallocator<ZcPlPlotConfigInfo> >* ptr2;
			try
			{
				if (ptr != null)
				{
					*(long*)ptr = 0L;
					*(int*)((ulong)(nint)ptr + 8uL) = 0;
					*(int*)((ulong)(nint)ptr + 12uL) = 0;
					*(int*)((ulong)(nint)ptr + 16uL) = 8;
					ptr2 = ptr;
				}
				else
				{
					ptr2 = null;
				}
			}
			catch
			{
				//try-fault
				global::<Module>.delete(ptr, 24uL);
				throw;
			}
			ZcPlPlotConfigManager* ptr3 = global::<Module>.zcplPlotConfigManagerPtr();
			if (ptr3 != null)
			{
				((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcArray<ZcPlPlotConfigInfo,ZcArrayObjectCopyReallocator<ZcPlPlotConfigInfo> >*, byte>)(*(ulong*)(*(ulong*)ptr3)))((nint)ptr3, ptr2);
			}
			IntPtr unmanagedPointer = new IntPtr(ptr2);
			return new PlotConfigInfoCollection(unmanagedPointer, autoDelete: true);
		}
	}

	public unsafe static PlotConfig CurrentConfig
	{
		get
		{
			//IL_0015: Expected I, but got I8
			ZcPlPlotConfigManager* ptr = global::<Module>.zcplPlotConfigManagerPtr();
			System.Runtime.CompilerServices.Unsafe.SkipInit(out ZcPlPlotConfig* value);
			Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcPlPlotConfig**, global::Zcad.ErrorStatus>)(*(ulong*)(*(long*)ptr + 24)))((nint)ptr, &value));
			IntPtr unmanagedPointer = new IntPtr(value);
			return new PlotConfig(unmanagedPointer, autoDelete: true);
		}
	}

	public unsafe static string StdConfigNames
	{
		get
		{
			//IL_0014: Expected I, but got I8
			ZcPlPlotConfigManager* ptr = global::<Module>.zcplPlotConfigManagerPtr();
			return new string(((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcPlPlotConfigManager.StdConfigs, char*>)(*(ulong*)(*(long*)ptr + 40)))((nint)ptr, (ZcPlPlotConfigManager.StdConfigs)config));
		}
	}

	public unsafe static StringCollection ColorDependentPlotStyles
	{
		get
		{
			//IL_002e: Expected I, but got I8
			//IL_004d: Expected I, but got I8
			//IL_0069: Expected I, but got I8
			System.Runtime.CompilerServices.Unsafe.SkipInit(out global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> > obj);
			*(long*)(&obj) = 0L;
			System.Runtime.CompilerServices.Unsafe.As<global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 8)) = 0;
			System.Runtime.CompilerServices.Unsafe.As<global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 12)) = 0;
			System.Runtime.CompilerServices.Unsafe.As<global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 16)) = 8;
			StringCollection stringCollection;
			try
			{
				ZcPlPlotConfigManager* ptr = global::<Module>.zcplPlotConfigManagerPtr();
				((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> >*, int, byte>)(*(ulong*)(*(long*)ptr + 8)))((nint)ptr, &obj, 4);
				stringCollection = new StringCollection();
				int num = 0;
				if (0 < System.Runtime.CompilerServices.Unsafe.As<global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 12)))
				{
					long num2 = 0L;
					do
					{
						char* value = (char*)(*(ulong*)(num2 + *(long*)(&obj)));
						stringCollection.Add(new string(value));
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

	public unsafe static StringCollection NamedPlotStyles
	{
		get
		{
			//IL_002e: Expected I, but got I8
			//IL_004d: Expected I, but got I8
			//IL_0069: Expected I, but got I8
			System.Runtime.CompilerServices.Unsafe.SkipInit(out global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> > obj);
			*(long*)(&obj) = 0L;
			System.Runtime.CompilerServices.Unsafe.As<global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 8)) = 0;
			System.Runtime.CompilerServices.Unsafe.As<global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 12)) = 0;
			System.Runtime.CompilerServices.Unsafe.As<global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 16)) = 8;
			StringCollection stringCollection;
			try
			{
				ZcPlPlotConfigManager* ptr = global::<Module>.zcplPlotConfigManagerPtr();
				((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> >*, int, byte>)(*(ulong*)(*(long*)ptr + 8)))((nint)ptr, &obj, 1);
				stringCollection = new StringCollection();
				int num = 0;
				if (0 < System.Runtime.CompilerServices.Unsafe.As<global::ZcArray<wchar_t *,ZcArrayMemCopyReallocator<wchar_t *> >, int>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref obj, 12)))
				{
					long num2 = 0L;
					do
					{
						char* value = (char*)(*(ulong*)(num2 + *(long*)(&obj)));
						stringCollection.Add(new string(value));
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

	public unsafe static void RefreshList(RefreshCode refreshCode)
	{
		//IL_0014: Expected I, but got I8
		ZcPlPlotConfigManager* ptr = global::<Module>.zcplPlotConfigManagerPtr();
		((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcPlPlotConfigManager.RefreshCode, void>)(*(ulong*)(*(long*)ptr + 16)))((nint)ptr, (ZcPlPlotConfigManager.RefreshCode)refreshCode);
	}

	public unsafe static PlotConfig SetCurrentConfig(string deviceName)
	{
		//IL_0015: Expected I, but got I8
		//IL_0026: Expected I, but got I8
		//IL_0047: Expected I, but got I8
		System.Runtime.CompilerServices.Unsafe.SkipInit(out global::StringToWchar stringToWchar);
		global::StringToWchar* ptr = global::<Module>.StringToWchar.{ctor}(&stringToWchar, deviceName);
		System.Runtime.CompilerServices.Unsafe.SkipInit(out ZcPlPlotConfig* value);
		try
		{
			ZcPlPlotConfigManager* ptr2 = global::<Module>.zcplPlotConfigManagerPtr();
			char* ptr3 = (char*)(*(ulong*)ptr);
			Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcPlPlotConfig**, char*, global::Zcad.ErrorStatus>)(*(ulong*)(*(long*)ptr2 + 32)))((nint)ptr2, &value, ptr3));
		}
		catch
		{
			//try-fault
			global::<Module>.___CxxCallUnwindDtor((delegate*<void*, void>)(delegate*<global::StringToWchar*, void>)(&global::<Module>.StringToWchar.{dtor}), &stringToWchar);
			throw;
		}
		IntPtr intPtr = new IntPtr((void*)System.Runtime.CompilerServices.Unsafe.As<global::StringToWchar, ulong>(ref System.Runtime.CompilerServices.Unsafe.AddByteOffset(ref stringToWchar, 8)));
		((GCHandle)intPtr).Free();
		IntPtr unmanagedPointer = new IntPtr(value);
		return new PlotConfig(unmanagedPointer, autoDelete: true);
	}

	private PlotConfigManager()
	{
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.0.0.9375' (yours is '8.2.0.7535-95108c96')
