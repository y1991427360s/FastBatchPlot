using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.Runtime;

namespace ZwSoft.ZwCAD.PlottingServices;

[Wrapper("AcPlPlotInfo")]
public sealed class PlotInfo : RXObject
{
	public unsafe int MergeStatus => (int)global::<Module>.ZcPlPlotInfo.mergeStatus(GetImpObj());

	public unsafe bool IsValidated
	{
		[return: MarshalAs(UnmanagedType.U1)]
		get
		{
			return (byte)(global::<Module>.ZcPlPlotInfo.isValidated(GetImpObj()) ? 1u : 0u) != 0;
		}
	}

	public unsafe PlotConfig ValidatedConfig
	{
		get
		{
			IntPtr unmanagedPointer = new IntPtr(global::<Module>.ZcPlPlotInfo.validatedConfig(GetImpObj()));
			return new PlotConfig(unmanagedPointer, autoDelete: false);
		}
		set
		{
			global::<Module>.ZcPlPlotInfo.setValidatedConfig(GetImpObj(), value.GetImpObj());
		}
	}

	public unsafe PlotSettings ValidatedSettings
	{
		get
		{
			return (PlotSettings)DisposableWrapper.Create(unmanagedPointer: new IntPtr(global::<Module>.ZcPlPlotInfo.validatedSettings(GetImpObj())), type: typeof(PlotSettings), autoDelete: false);
		}
		set
		{
			IntPtr unmanagedObject = value.UnmanagedObject;
			Interop.Check((int)global::<Module>.ZcPlPlotInfo.setValidatedSettings(GetImpObj(), (global::ZcDbPlotSettings*)unmanagedObject.ToPointer()));
		}
	}

	public unsafe PlotConfig DeviceOverride
	{
		get
		{
			IntPtr unmanagedPointer = new IntPtr(global::<Module>.ZcPlPlotInfo.deviceOverride(GetImpObj()));
			return new PlotConfig(unmanagedPointer, autoDelete: false);
		}
		set
		{
			global::<Module>.ZcPlPlotInfo.setDeviceOverride(GetImpObj(), value.GetImpObj());
		}
	}

	public unsafe PlotSettings OverrideSettings
	{
		get
		{
			return (PlotSettings)DisposableWrapper.Create(unmanagedPointer: new IntPtr(global::<Module>.ZcPlPlotInfo.overrideSettings(GetImpObj())), type: typeof(PlotSettings), autoDelete: false);
		}
		set
		{
			IntPtr unmanagedObject = value.UnmanagedObject;
			global::<Module>.ZcPlPlotInfo.setOverrideSettings(GetImpObj(), (global::ZcDbPlotSettings*)unmanagedObject.ToPointer());
		}
	}

	public unsafe ObjectId Layout
	{
		get
		{
			ObjectId objectId = default(ObjectId);
			System.Runtime.CompilerServices.Unsafe.SkipInit(out global::ZcDbObjectId zcDbObjectId);
			global::ZcDbObjectId* ptr = global::<Module>.ZcPlPlotInfo.layout(GetImpObj(), &zcDbObjectId);
			IntPtr oldId = new IntPtr(*(long*)ptr);
			return new ObjectId(oldId);
		}
		set
		{
			System.Runtime.CompilerServices.Unsafe.SkipInit(out global::ZcDbObjectId zcDbObjectId);
			*(long*)(&zcDbObjectId) = 0L;
			*(long*)(&zcDbObjectId) = value.OldId;
			global::<Module>.ZcPlPlotInfo.setLayout(GetImpObj(), &zcDbObjectId);
		}
	}

	internal unsafe ZcPlPlotInfo* GetImpObj()
	{
		return (ZcPlPlotInfo*)base.UnmanagedObject.ToPointer();
	}

	public unsafe PlotInfo()
	{
		//IL_000a: Expected I, but got I8
		//IL_001c: Expected I, but got I8
		void* ptr = global::<Module>.zcHeapAlloc(null, 16uL);
		ZcPlPlotInfo* pThis = (ZcPlPlotInfo*)ptr;
		ZcPlPlotInfo* value;
		try
		{
			value = ((ptr == null) ? null : global::<Module>.ZcPlPlotInfo.{ctor}((ZcPlPlotInfo*)ptr));
		}
		catch
		{
			//try-fault
			global::<Module>.___CxxCallUnwindDelDtor((delegate*<void*, void>)(&global::<Module>.ZcHeapOperators.delete), (void*)pThis);
			throw;
		}
		base..ctor(new IntPtr(value), autoDelete: true);
	}

	internal PlotInfo(IntPtr unmanagedPointer, [MarshalAs(UnmanagedType.U1)] bool autoDelete)
		: base(unmanagedPointer, autoDelete)
	{
	}

	public unsafe void CopyToUnmanagedObject(IntPtr unmanagedPointer)
	{
		//IL_001b: Expected I, but got I8
		void* ptr = unmanagedPointer.ToPointer();
		((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, global::ZcRxObject*, global::Zcad.ErrorStatus>)(*(ulong*)(*(long*)ptr + 24)))((nint)ptr, (global::ZcRxObject*)GetImpObj());
	}

	public unsafe static PlotInfo CopyFromUnmanagedObject(IntPtr unmanagedPointer)
	{
		//IL_000a: Expected I, but got I8
		//IL_0051: Expected I, but got I8
		void* ptr = global::<Module>.zcHeapAlloc(null, 16uL);
		ZcPlPlotInfo* pThis = (ZcPlPlotInfo*)ptr;
		ZcPlPlotInfo* value;
		try
		{
			if (ptr != null)
			{
				ZcPlPlotInfo* ptr2 = (ZcPlPlotInfo*)unmanagedPointer.ToPointer();
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
					*(long*)ptr = (nint)System.Runtime.CompilerServices.Unsafe.AsPointer(ref global::<Module>.??_7ZcPlPlotInfo@@6B@);
				}
				catch
				{
					//try-fault
					global::<Module>.___CxxCallUnwindDtor((delegate*<void*, void>)(delegate*<ZcPlObject*, void>)(&global::<Module>.ZcPlObject.{dtor}), pThis);
					throw;
				}
				value = (ZcPlPlotInfo*)ptr;
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
		return new PlotInfo(unmanagedPointer2, autoDelete: true);
	}

	[return: MarshalAs(UnmanagedType.U1)]
	public unsafe bool IsCompatibleDocument(PlotInfo info)
	{
		return (byte)(global::<Module>.ZcPlPlotInfo.isCompatibleDocument(GetImpObj(), info.GetImpObj()) ? 1u : 0u) != 0;
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.0.0.9375' (yours is '8.2.0.7535-95108c96')
