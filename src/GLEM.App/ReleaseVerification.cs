using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using GLEM.App.ViewModels;
using GLEM.Core.Models;

namespace GLEM.App;

// M4 配布物検証（実装計画書 4-2/4-3、M4 DoD「クリーン環境で起動し C-01〜C-03 を再確認」）:
//   --capture <dir> : ユーザーマニュアル用の画面キャプチャを PNG で生成する
//   --selftest      : 配布バイナリ上で C-01〜C-03 のフローを再検証し、終了コードで結果を返す
public static class ReleaseVerification
{
    public static bool CaptureScreenshots(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);

            var vm = new MainViewModel();

            // 前回の異常終了時の自動保存が残っていると復元ダイアログが出るため、キャプチャ前に破棄する
            if (vm.HasPendingAutosave)
            {
                vm.DiscardAutosave();
            }

            // S-2: 検証合格状態を表示する
            ((IRelayCommand)vm.GroundModelEditor.RunValidationCommand).Execute(null);

            // S-4: 円弧滑動面を持つ結果（Bishop・限定探索範囲）を先に生成する
            vm.SlopeAnalysis.AutoSearch = false;
            vm.SlopeAnalysis.CxMin = -8.0;
            vm.SlopeAnalysis.CxMax = 8.0;
            vm.SlopeAnalysis.CzMin = -10.0;
            vm.SlopeAnalysis.CzMax = -2.0;
            vm.SlopeAnalysis.RadiusMin = 5.0;
            vm.SlopeAnalysis.RadiusMax = 12.0;
            ((IAsyncRelayCommand)vm.SlopeAnalysis.RunCommand).ExecuteAsync(null).GetAwaiter().GetResult();

            // S-3: Janbu 選択 + 制御点エディタを表示する（結果は上記の円弧のまま）
            vm.SlopeAnalysis.Method = SlopeMethod.JanbuGeneralized;
            vm.SlopeAnalysis.ControlPoints.Add(new ControlPointRow { X = -5.0, Z = 1.0 });
            vm.SlopeAnalysis.ControlPoints.Add(new ControlPointRow { X = -2.0, Z = 3.5 });
            vm.SlopeAnalysis.ControlPoints.Add(new ControlPointRow { X = 2.0, Z = 4.5 });
            vm.SlopeAnalysis.ControlPoints.Add(new ControlPointRow { X = 6.0, Z = 1.5 });

            // S-6: 沈下解析結果を生成する（全層に k/e0/Cc を設定）
            foreach (var row in vm.GroundModelEditor.Layers)
            {
                row.PermeabilityMs ??= 1e-8;
                row.InitialVoidRatio ??= 1.2;
                row.CompressionIndexCc ??= 0.25;
                row.SecondaryCompressionIndexCs = 0.02;
                row.ElasticModulusKpa ??= 20000.0;
            }

            ((IAsyncRelayCommand)vm.Settlement.RunCommand).ExecuteAsync(null).GetAwaiter().GetResult();

            var window = new Views.MainWindow(vm) { Width = 1280, Height = 800 };

            // 未表示の Window は RenderTargetBitmap で描画されないため、画面外で Show しディスパッチャーをポンプする
            window.Left = -4000;
            window.Top = -200;
            window.Show();
            PumpUntilRendered(window);

            Capture(window, vm, Screen.GroundModel, Path.Combine(dir, "s2-ground-model.png"));
            Capture(window, vm, Screen.SlopeSettings, Path.Combine(dir, "s3-slope-settings-janbu.png"));
            Capture(window, vm, Screen.SlopeResult, Path.Combine(dir, "s4-slope-result.png"));
            Capture(window, vm, Screen.SettlementSettings, Path.Combine(dir, "s5-settlement-settings.png"));
            Capture(window, vm, Screen.SettlementResult, Path.Combine(dir, "s6-settlement-result.png"));

            window.Close();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CAPTURE-FAIL] {ex}");
            return false;
        }
    }

    private static void Capture(Window window, MainViewModel vm, Screen screen, string path)
    {
        const int width = 1280;
        const int height = 800;

        vm.ActiveScreen = screen;
        Pump(window);

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(window);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(path);
        encoder.Save(fs);
        Console.WriteLine($"[CAPTURE-OK] {Path.GetFileName(path)}");
    }

    // ディスパッチャーキューをポンプし、レイアウト・レンダリングが完了するまで待つ
    private static void PumpUntilRendered(Window window)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 5000 && window.ActualWidth == 0)
        {
            Pump(window);
        }

        if (window.ActualWidth == 0)
        {
            throw new InvalidOperationException("Window did not complete layout within 5 s");
        }
    }

    private static void Pump(Window window)
    {
        for (var i = 0; i < 3; i++)
        {
            // 同一ディスパッチャースレッドからの Wait() はネストしたポンプとして動作する
            var op = System.Windows.Threading.Dispatcher.CurrentDispatcher
                .InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Send);
            op.Wait(TimeSpan.FromMilliseconds(500));
        }
    }

    public static bool RunSelfTest()
    {
        var allOk = true;

        // C-01: 新規プロジェクト→層2件→検証合格
        try
        {
            var vm = new MainViewModel();
            Check(vm.GroundModelEditor.Layers.Count == 2, "default project has 2 layers");
            ((IRelayCommand)vm.GroundModelEditor.RunValidationCommand).Execute(null);
            Check(!vm.GroundModelEditor.HasValidationErrors, "validation passes on the default project");
            Console.WriteLine("[PASS] C-01 new project -> 2 layers -> validation passed");
        }
        catch (Exception ex)
        {
            allOk = false;
            Console.WriteLine($"[FAIL] C-01: {ex.Message}");
        }

        // C-02: 違反入力→該当セルハイライト+仕様メッセージ
        try
        {
            var vm = new MainViewModel();
            vm.GroundModelEditor.Layers[0].ThicknessM = -5.0;
            ((IRelayCommand)vm.GroundModelEditor.RunValidationCommand).Execute(null);
            Check(vm.GroundModelEditor.HasValidationErrors, "invalid thickness is flagged");
            Check(vm.GroundModelEditor.LastIssues.Any(i => i.Code == "GLEM-1002"), "message code GLEM-1002 (spec §3.4)");
            Check(vm.GroundModelEditor.Layers[0].ErrorFields.Contains("thickness_m"), "cell highlight target thickness_m");
            Console.WriteLine("[PASS] C-02 invalid input -> cell highlighted + spec message");
        }
        catch (Exception ex)
        {
            allOk = false;
            Console.WriteLine($"[FAIL] C-02: {ex.Message}");
        }

        // C-03: 手法切替で FS が再計算される（Fellenius/Bishop/Janbu）
        try
        {
            var vm = new MainViewModel();
            vm.SlopeAnalysis.AutoSearch = false;
            vm.SlopeAnalysis.CxMin = -4.0;
            vm.SlopeAnalysis.CxMax = 4.0;
            vm.SlopeAnalysis.CzMin = -6.0;
            vm.SlopeAnalysis.CzMax = -2.0;
            vm.SlopeAnalysis.RadiusMin = 5.0;
            vm.SlopeAnalysis.RadiusMax = 7.0;

            foreach (var method in new[] { SlopeMethod.Fellenius, SlopeMethod.BishopSimplified, SlopeMethod.JanbuGeneralized })
            {
                vm.SlopeAnalysis.Method = method;
                ((IAsyncRelayCommand)vm.SlopeAnalysis.RunCommand).ExecuteAsync(null).GetAwaiter().GetResult();
                Check(vm.SlopeAnalysis.Result is not null && vm.SlopeAnalysis.Result.MinFs > 0.3, $"FS computed for {method}");
            }

            Console.WriteLine("[PASS] C-03 method switching -> FS recomputed (Fellenius/Bishop/Janbu)");
        }
        catch (Exception ex)
        {
            allOk = false;
            Console.WriteLine($"[FAIL] C-03: {ex.Message}");
        }

        Console.WriteLine(allOk ? "SELFTEST RESULT: PASS" : "SELFTEST RESULT: FAIL");
        return allOk;
    }

    private static void Check(bool condition, string what)
    {
        if (!condition)
        {
            throw new InvalidOperationException(what);
        }
    }
}
