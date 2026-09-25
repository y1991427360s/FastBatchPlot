using System;
using System.Runtime.InteropServices;
using ZwSoft.ZwCAD.Runtime;

namespace ZwSoft.ZwCAD.PlottingServices;

[Wrapper("AcPlPlotInfoValidator")]
public sealed class PlotInfoValidator : RXObject
{
	public unsafe int MediaMatchingThreshold
	{
		get
		{
			return (int)global::<Module>.ZcPlPlotInfoValidator.mediaMatchingThreshold(GetImpObj());
		}
		set
		{
			global::<Module>.ZcPlPlotInfoValidator.setMediaMatchingThreshold(GetImpObj(), (uint)value);
		}
	}

	public unsafe int SheetDimensionalWeight
	{
		get
		{
			return (int)global::<Module>.ZcPlPlotInfoValidator.sheetDimensionalWeight(GetImpObj());
		}
		set
		{
			global::<Module>.ZcPlPlotInfoValidator.setSheetDimensionalWeight(GetImpObj(), (uint)value);
		}
	}

	public unsafe int DimensionalWeight
	{
		get
		{
			return (int)global::<Module>.ZcPlPlotInfoValidator.dimensionalWeight(GetImpObj());
		}
		set
		{
			global::<Module>.ZcPlPlotInfoValidator.setDimensionalWeight(GetImpObj(), (uint)value);
		}
	}

	public unsafe int PrintableBoundsWeight
	{
		get
		{
			return (int)global::<Module>.ZcPlPlotInfoValidator.printableBoundsWeight(GetImpObj());
		}
		set
		{
			global::<Module>.ZcPlPlotInfoValidator.setPrintableBoundsWeight(GetImpObj(), (uint)value);
		}
	}

	public unsafe int MediaBoundsWeight
	{
		get
		{
			return (int)global::<Module>.ZcPlPlotInfoValidator.mediaBoundsWeight(GetImpObj());
		}
		set
		{
			global::<Module>.ZcPlPlotInfoValidator.setMediaBoundsWeight(GetImpObj(), (uint)value);
		}
	}

	public unsafe int SheetMediaGroupWeight
	{
		get
		{
			return (int)global::<Module>.ZcPlPlotInfoValidator.sheetMediaGroupWeight(GetImpObj());
		}
		set
		{
			global::<Module>.ZcPlPlotInfoValidator.setSheetMediaGroupWeight(GetImpObj(), (uint)value);
		}
	}

	public unsafe int MediaGroupWeight
	{
		get
		{
			return (int)global::<Module>.ZcPlPlotInfoValidator.mediaGroupWeight(GetImpObj());
		}
		set
		{
			global::<Module>.ZcPlPlotInfoValidator.setMediaGroupWeight(GetImpObj(), (uint)value);
		}
	}

	public unsafe MatchingPolicy MediaMatchingPolicy
	{
		get
		{
			return (MatchingPolicy)global::<Module>.ZcPlPlotInfoValidator.matchingPolicy(GetImpObj());
		}
		set
		{
			global::<Module>.ZcPlPlotInfoValidator.setMediaMatchingPolicy(GetImpObj(), (ZcPlPlotInfoValidator.MatchingPolicy)value);
		}
	}

	internal unsafe ZcPlPlotInfoValidator* GetImpObj()
	{
		return (ZcPlPlotInfoValidator*)base.UnmanagedObject.ToPointer();
	}

	public unsafe PlotInfoValidator()
	{
		//IL_000a: Expected I, but got I8
		//IL_001c: Expected I, but got I8
		void* ptr = global::<Module>.zcHeapAlloc(null, 48uL);
		ZcPlPlotInfoValidator* pThis = (ZcPlPlotInfoValidator*)ptr;
		ZcPlPlotInfoValidator* value;
		try
		{
			value = ((ptr == null) ? null : global::<Module>.ZcPlPlotInfoValidator.{ctor}((ZcPlPlotInfoValidator*)ptr));
		}
		catch
		{
			//try-fault
			global::<Module>.___CxxCallUnwindDelDtor((delegate*<void*, void>)(&global::<Module>.ZcHeapOperators.delete), (void*)pThis);
			throw;
		}
		base..ctor(new IntPtr(value), autoDelete: true);
	}

	internal PlotInfoValidator(IntPtr unmanagedPointer, [MarshalAs(UnmanagedType.U1)] bool autoDelete)
		: base(unmanagedPointer, autoDelete)
	{
	}

	public unsafe static PlotInfoValidator CopyFromUnmanagedObject(IntPtr unmanagedPointer)
	{
		//IL_000a: Expected I, but got I8
		//IL_0023: Expected I, but got I8
		void* ptr = global::<Module>.zcHeapAlloc(null, 48uL);
		ZcPlPlotInfoValidator* pThis = (ZcPlPlotInfoValidator*)ptr;
		ZcPlPlotInfoValidator* value;
		try
		{
			value = ((ptr == null) ? null : global::<Module>.ZcPlPlotInfoValidator.{ctor}((ZcPlPlotInfoValidator*)ptr, (ZcPlPlotInfoValidator*)unmanagedPointer.ToPointer()));
		}
		catch
		{
			//try-fault
			global::<Module>.___CxxCallUnwindDelDtor((delegate*<void*, void>)(&global::<Module>.ZcHeapOperators.delete), (void*)pThis);
			throw;
		}
		IntPtr unmanagedPointer2 = new IntPtr(value);
		return new PlotInfoValidator(unmanagedPointer2, autoDelete: true);
	}

	public unsafe void Validate(PlotInfo info)
	{
		//IL_001a: Expected I, but got I8
		ZcPlPlotInfoValidator* impObj = GetImpObj();
		Interop.Check((int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcPlPlotInfo*, global::Zcad.ErrorStatus>)(*(ulong*)(*(long*)impObj + 56)))((nint)impObj, info.GetImpObj()));
	}

	public unsafe int IsCustomPossible(PlotInfo info)
	{
		//IL_001a: Expected I, but got I8
		ZcPlPlotInfoValidator* impObj = GetImpObj();
		return (int)((delegate* unmanaged[Cdecl, Cdecl]<IntPtr, ZcPlPlotInfo*, uint>)(*(ulong*)(*(long*)impObj + 64)))((nint)impObj, info.GetImpObj());
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.0.0.9375' (yours is '8.2.0.7535-95108c96')
