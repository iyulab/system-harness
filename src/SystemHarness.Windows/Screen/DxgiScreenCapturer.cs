using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DXGI.DXGI;
using DxgiResultCode = Vortice.DXGI.ResultCode;
using PInvoke = global::Windows.Win32.PInvoke;
using SYSTEM_METRICS_INDEX = global::Windows.Win32.UI.WindowsAndMessaging.SYSTEM_METRICS_INDEX;

namespace SystemHarness.Windows;

/// <summary>
/// GPU-accelerated screen capture using DXGI Desktop Duplication (Win8+).
/// IDisposable — holds COM resources that must be released.
/// </summary>
internal sealed class DxgiScreenCapturer : IDisposable
{
    private IDXGIFactory1? _factory;
    private IDXGIAdapter1? _adapter;
    private IDXGIOutput? _output;
    private IDXGIOutput1? _output1;
    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGIOutputDuplication? _duplication;
    private ID3D11Texture2D? _stagingTexture;
    private int _width;
    private int _height;
    private int _disposed;

    /// <summary>
    /// Attempts to initialize DXGI Desktop Duplication for the primary monitor.
    /// Returns null if initialization fails (RDP, no GPU, etc.).
    /// </summary>
    public static DxgiScreenCapturer? TryCreate()
    {
        if (IsRemoteSession())
            return null;

        try
        {
            var capturer = new DxgiScreenCapturer();
            capturer.Initialize();
            return capturer;
        }
        catch
        {
            return null;
        }
    }

    private void Initialize()
    {
        _factory = CreateDXGIFactory1<IDXGIFactory1>();

        // Find first adapter with an output
        for (uint adapterIdx = 0;
             _factory.EnumAdapters1(adapterIdx, out IDXGIAdapter1? adapter).Success;
             adapterIdx++)
        {
            if (adapter.EnumOutputs(0, out IDXGIOutput? output).Success)
            {
                _adapter = adapter;
                _output = output;
                break;
            }
            adapter.Dispose();
        }

        if (_adapter is null || _output is null)
            throw new HarnessException("No display output found");

        // Create D3D11 device on the same adapter
        D3D11CreateDevice(
            _adapter,
            DriverType.Unknown,
            DeviceCreationFlags.BgraSupport,
            [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0],
            out _device,
            out _,
            out _context
        ).CheckError();

        // Duplicate output
        _output1 = _output.QueryInterface<IDXGIOutput1>();
        _duplication = _output1.DuplicateOutput(_device);

        // Read output dimensions
        var desc = _output.Description;
        _width = desc.DesktopCoordinates.Right - desc.DesktopCoordinates.Left;
        _height = desc.DesktopCoordinates.Bottom - desc.DesktopCoordinates.Top;

        // Create staging texture for CPU readback
        _stagingTexture = _device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)_width,
            Height = (uint)_height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
        });
    }

    /// <summary>
    /// Captures the current desktop frame.
    /// Returns raw BGRA pixel data or null if no frame available.
    /// </summary>
    public byte[]? CaptureFrame(out int width, out int height)
    {
        width = _width;
        height = _height;

        if (_duplication is null || _context is null || _stagingTexture is null)
            return null;

        IDXGIResource? resource = null;
        try
        {
            var result = _duplication.AcquireNextFrame(500, out _, out resource);

            if (result.Failure)
            {
                if (result.Code == DxgiResultCode.WaitTimeout.Code)
                    return null;

                if (result.Code == DxgiResultCode.AccessLost.Code)
                {
                    RecreateDuplication();
                    return null;
                }

                return null;
            }

            using var desktopTexture = resource!.QueryInterface<ID3D11Texture2D>();
            _context.CopyResource(_stagingTexture, desktopTexture);
        }
        finally
        {
            resource?.Dispose();
            try { _duplication?.ReleaseFrame(); } catch { /* frame may not have been acquired */ }
        }

        // Map staging texture for CPU read
        var mapped = _context.Map(_stagingTexture, 0, MapMode.Read);
        try
        {
            int srcPitch = (int)mapped.RowPitch;
            int dstPitch = _width * 4;
            var pixelData = new byte[dstPitch * _height];

            unsafe
            {
                byte* src = (byte*)mapped.DataPointer;
                fixed (byte* dst = pixelData)
                {
                    if (srcPitch == dstPitch)
                    {
                        Buffer.MemoryCopy(src, dst, pixelData.Length, pixelData.Length);
                    }
                    else
                    {
                        for (int row = 0; row < _height; row++)
                            Buffer.MemoryCopy(src + row * srcPitch, dst + row * dstPitch, dstPitch, dstPitch);
                    }
                }
            }

            return pixelData;
        }
        finally
        {
            _context.Unmap(_stagingTexture, 0);
        }
    }

    private void RecreateDuplication()
    {
        _duplication?.Dispose();
        _duplication = null;

        Thread.Sleep(100);

        try
        {
            if (_output1 is not null && _device is not null)
                _duplication = _output1.DuplicateOutput(_device);
        }
        catch
        {
            // Duplication recreation failed — will return null on next capture
        }
    }

    private static bool IsRemoteSession()
    {
        return PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_REMOTESESSION) != 0;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _duplication?.Dispose();
        _stagingTexture?.Dispose();
        _context?.Dispose();
        _device?.Dispose();
        _output1?.Dispose();
        _output?.Dispose();
        _adapter?.Dispose();
        _factory?.Dispose();
    }
}
