namespace RTCV.CorruptCore
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Newtonsoft.Json;
    using RTCV.Common.CustomExtensions;
    using RTCV.NetCore;
    using RTCV.PluginHost;
    using BlastRadiusAlgorithm = System.Func<RTCV.CorruptCore.BlastInfo, RTCV.CorruptCore.BlastLayer>;
    using Timer = System.Windows.Forms.Timer;

    public class ProblematicProcess
    {
        public string Name { get; set; }
        public string Message { get; set; }
    }

    public static class RtcCore
    {
        //General RTC Values
        public const string RtcVersion = "5.0.5-rc";
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private static volatile int seed = DateTime.Now.Millisecond;
        public static int Seed => ++seed;

        [ThreadStatic]
        private static Random _RND = null;
        public static Random RND => _RND ??= new Random(Seed);

        public static bool Attached = false;

        public static int CachedPrecision;

        public static List<ProblematicProcess> ProblematicProcesses;

        public static Timer KillswitchTimer = new Timer();

        private static readonly PluginHost.Host pluginHost = new PluginHost.Host();
        public static PluginHost.Host PluginHost => pluginHost;

        public static bool EmuDirOverride = false;

        public static string EmuDir
        {
            get
            {
                //In attached mode we can just use the directory we're in.
                //We do this as the EmuDir is not set in attached
                if (Attached || EmuDirOverride)
                {
                    return Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                }

                return (string)AllSpec.VanguardSpec?[VSPEC.EMUDIR];
            }
            set => AllSpec.VanguardSpec.Update(VSPEC.EMUDIR, value);
        }

        public static string EmuAssetsDir => Path.Combine(EmuDir, "ASSETS");
        public static string PluginDir => Path.Combine(RtcDir, "PLUGINS");

        public static string RtcDir
        {
            get => (string)AllSpec.CorruptCoreSpec[RTCSPEC.RTCDIR];
            set => AllSpec.CorruptCoreSpec.Update(RTCSPEC.RTCDIR, value);
        }

        public static string workingDir => Path.Combine(RtcDir, "WORKING");
        public static string AssetsDir => Path.Combine(RtcDir, "ASSETS");
        public static string ListsDir => Path.Combine(RtcDir, "LISTS");

        public static string VmdsDir => Path.Combine(RtcDir, "VMDS");
        public static string EngineTemplateDir => Path.Combine(RtcDir, "ENGINETEMPLATES");

        public static event EventHandler<ProgressBarEventArgs> ProgressBarHandler;

        //This is for the UI only but needs to be in here as well
        public static BindingList<ComboBoxItem<string>> LimiterListBindingSource = new BindingList<ComboBoxItem<string>>();
        public static BindingList<ComboBoxItem<string>> ValueListBindingSource = new BindingList<ComboBoxItem<string>>();

        public static bool AllowCrossCoreCorruption
        {
            get => (bool)AllSpec.CorruptCoreSpec[RTCSPEC.CORE_ALLOWCROSSCORECORRUPTION];
            set => AllSpec.CorruptCoreSpec.Update(RTCSPEC.CORE_ALLOWCROSSCORECORRUPTION, value);
        }

        public static CorruptionEngine SelectedEngine
        {
            get => (CorruptionEngine)AllSpec.CorruptCoreSpec[RTCSPEC.CORE_SELECTEDENGINE];
            set => AllSpec.CorruptCoreSpec.Update(RTCSPEC.CORE_SELECTEDENGINE, value);
        }

        public static int CurrentPrecision
        {
            get => (int)AllSpec.CorruptCoreSpec[RTCSPEC.CORE_CURRENTPRECISION];
            set => AllSpec.CorruptCoreSpec.Update(RTCSPEC.CORE_CURRENTPRECISION, value);
        }

        public static int Alignment
        {
            get => (int)AllSpec.CorruptCoreSpec[RTCSPEC.CORE_CURRENTALIGNMENT];
            set => AllSpec.CorruptCoreSpec.Update(RTCSPEC.CORE_CURRENTALIGNMENT, value);
        }

        public static long Intensity
        {
            get => (long)AllSpec.CorruptCoreSpec?[RTCSPEC.CORE_INTENSITY];
            set => AllSpec.CorruptCoreSpec.Update(RTCSPEC.CORE_INTENSITY, value);
        }

        public static long ErrorDelay
        {
            get => (long)AllSpec.CorruptCoreSpec?[RTCSPEC.CORE_ERRORDELAY];
            set => AllSpec.CorruptCoreSpec.Update(RTCSPEC.CORE_ERRORDELAY, value);
        }

        public static BlastRadiusAlgorithm Radius
        {
            get => BlastRadius.GetFromString((string)AllSpec.CorruptCoreSpec[RTCSPEC.CORE_RADIUS]);
            set => AllSpec.CorruptCoreSpec.Update(RTCSPEC.CORE_RADIUS, value.ToString());
        }

        public static bool AutoCorrupt
        {
            get => (bool)(AllSpec.CorruptCoreSpec?[RTCSPEC.CORE_AUTOCORRUPT] ?? false);
            set => AllSpec.CorruptCoreSpec.Update(RTCSPEC.CORE_AUTOCORRUPT, value);
        }

        public static bool DontCleanSavestatesOnQuit
        {
            get => (bool)(AllSpec.CorruptCoreSpec[RTCSPEC.CORE_DONTCLEANSAVESTATESONQUIT] ?? false);
            set => AllSpec.CorruptCoreSpec.Update(RTCSPEC.CORE_DONTCLEANSAVESTATESONQUIT, value);
        }

        public static bool ShowConsole
        {
            get => (bool)AllSpec.CorruptCoreSpec[RTCSPEC.CORE_SHOWCONSOLE];
            set => AllSpec.CorruptCoreSpec.Update(RTCSPEC.CORE_SHOWCONSOLE, value);
        }

        public static bool RerollAddress
        {
            get => (bool)AllSpec.CorruptCoreSpec[RTCSPEC.CORE_REROLLADDRESS];
            set
            {
                AllSpec.CorruptCoreSpec.Update(RTCSPEC.CORE_REROLLADDRESS, value);
                Params.SetParam("REROLL_ADDRESS", value.ToString());
            }
        }

        public static bool RerollSourceAddress
        {
            get => (bool)AllSpec.CorruptCoreSpec[RTCSPEC.CORE_REROLLSOURCEADDRESS];
            set
            {
                AllSpec.CorruptCoreSpec.Update(RTCSPEC.CORE_REROLLSOURCEADDRESS, value);
                Params.SetParam("REROLL_SOURCEADDRESS", value.ToString());
            }
        }

        public static bool RerollDomain
        {
            get => (bool)AllSpec.CorruptCoreSpec[RTCSPEC.CORE_REROLLDOMAIN];
            set
            {
                AllSpec.CorruptCoreSpec.Update(RTCSPEC.CORE_REROLLDOMAIN, value);
                Params.SetParam("REROLL_DOMAIN", value.ToString());
            }
        }

        public static bool RerollSourceDomain
        {
            get => (bool)AllSpec.CorruptCoreSpec[RTCSPEC.CORE_REROLLSOURCEDOMAIN];
            set
            {
                AllSpec.CorruptCoreSpec.Update(RTCSPEC.CORE_REROLLSOURCEDOMAIN, value);
                Params.SetParam("REROLL_SOURCEDOMAIN", value.ToString());
            }
        }

        public static bool RerollIgnoresOriginalSource
        {
            get => (bool)AllSpec.CorruptCoreSpec[RTCSPEC.CORE_REROLLIGNOREORIGINALSOURCE];
            set
            {
                AllSpec.CorruptCoreSpec.Update(RTCSPEC.CORE_REROLLIGNOREORIGINALSOURCE, value);
                Params.SetParam("REROLL_IGNOREORIGINALSOURCE", value.ToString());
            }
        }

        public static bool RerollFollowsCustomEngine
        {
            get => (bool)AllSpec.CorruptCoreSpec[RTCSPEC.CORE_REROLLFOLLOWENGINESETTINGS];
            set
            {
                AllSpec.CorruptCoreSpec.Update(RTCSPEC.CORE_REROLLFOLLOWENGINESETTINGS, value);
                Params.SetParam("REROLL_FOLLOWSCUSTOMENGINE", value.ToString());
            }
        }

        public static bool ExtractBlastlayer
        {
            get => (bool)AllSpec.CorruptCoreSpec[RTCSPEC.CORE_EXTRACTBLASTLAYER];
            set => AllSpec.CorruptCoreSpec.Update(RTCSPEC.CORE_EXTRACTBLASTLAYER, value);
        }

        public static bool EmulatorOsdDisabled
        {
            get => (bool)AllSpec.CorruptCoreSpec[RTCSPEC.CORE_EMULATOROSDDISABLED];
            set => AllSpec.CorruptCoreSpec.Update(RTCSPEC.CORE_EMULATOROSDDISABLED, value);
        }

        public static string VanguardImplementationName => (string)AllSpec.VanguardSpec?[VSPEC.NAME] ?? "Vanguard Implementation";

        public static bool IsStandaloneUI;
        public static bool IsEmulatorSide;

        public class GameClosedEventArgs : EventArgs
        {
            public bool FullyClosed;
            public GameClosedEventArgs(bool fullyClosed)
            {
                FullyClosed = fullyClosed;
            }
        }
        public static EventHandler CorruptCoreExiting;
        public static EventHandler<GameClosedEventArgs> GameClosed;
        public static EventHandler LoadGameDone;

        public static void Start()
        {
        }

        public static void Shutdown()
        {
            CorruptCoreExiting?.Invoke(null, null);
            PluginHost.Shutdown();
        }

        private static void OneTimeSettingsInitialize()
        {
            RtcCore.RerollSourceAddress = true;
            RtcCore.RerollSourceDomain = true;
            RtcCore.RerollFollowsCustomEngine = true;
            Params.SetParam(RTCSPEC.CORE_EMULATOROSDDISABLED);
        }

        public static void StartUISide()
        {
            try
            {
                Start();
                RegisterCorruptcoreSpec();

                CorruptCore_Extensions.DirectoryRequired(paths: new string[] {
                    RtcCore.workingDir,
                    Path.Combine(RtcCore.workingDir, "TEMP"),
                    Path.Combine(RtcCore.workingDir, "SKS"),
                    Path.Combine(RtcCore.workingDir, "SSK"),
                    Path.Combine(RtcCore.workingDir, "SESSION"),
                    Path.Combine(RtcCore.workingDir, "MEMORYDUMPS"),
                    Path.Combine(RtcCore.workingDir, "MP"),
                    Path.Combine(RtcCore.AssetsDir, "CRASHSOUNDS"),
                    Path.Combine(RtcCore.RtcDir, "PARAMS"),
                    Path.Combine(RtcCore.RtcDir, "LISTS"),
                    Path.Combine(RtcCore.RtcDir, "RENDEROUTPUT"),
                    Path.Combine(RtcCore.RtcDir, "ENGINETEMPLATES"),
                    Path.Combine(RtcCore.AssetsDir, "PLATESHD")
                });

                if (!Params.IsParamSet("DISCLAIMER_READ"))
                {
                    OneTimeSettingsInitialize();
                }

                IsStandaloneUI = true;
            }
            catch (Exception ex)
            {
                if (CloudDebug.ShowErrorDialog(ex, true) == DialogResult.Abort)
                {
                    throw new AbortEverythingException();
                }
            }
        }

        public static void StartEmuSide()
        {
            if (!Attached)
            {
                Start();
                if (KillswitchTimer == null)
                {
                    KillswitchTimer = new Timer();
                }

                KillswitchTimer.Interval = 250;
                KillswitchTimer.Tick += KillswitchTimer_Tick;
                KillswitchTimer.Start();
            }
            IsEmulatorSide = true;
        }

        private static void KillswitchTimer_Tick(object sender, EventArgs e)
        {
            LocalNetCoreRouter.Route(NetcoreCommands.UI, NetcoreCommands.KILLSWITCH_PULSE);
        }

        /**
        * Register the spec on the rtc side
        */
        public static void RegisterCorruptcoreSpec()
        {
            try
            {
                var rtcSpecTemplate = new PartialSpec("RTCSpec");
                rtcSpecTemplate["RTCVERSION"] = RtcVersion;

                rtcSpecTemplate[RTCSPEC.RTCDIR] = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "RTC");

                //Engine Settings
                rtcSpecTemplate.Insert(RtcCore.getDefaultPartial());
                rtcSpecTemplate.Insert(RTC_NightmareEngine.getDefaultPartial());
                rtcSpecTemplate.Insert(RTC_HellgenieEngine.getDefaultPartial());
                rtcSpecTemplate.Insert(RTC_DistortionEngine.getDefaultPartial());

                //Custom Engine Config with Nightmare Engine
                RTC_CustomEngine.getDefaultPartial(rtcSpecTemplate);

                rtcSpecTemplate.Insert(StepActions.getDefaultPartial());
                rtcSpecTemplate.Insert(Filtering.getDefaultPartial());
                rtcSpecTemplate.Insert(RTC_VectorEngine.getDefaultPartial());
                rtcSpecTemplate.Insert(RTC_ClusterEngine.getDefaultPartial());
                rtcSpecTemplate.Insert(MemoryDomains.getDefaultPartial());
                rtcSpecTemplate.Insert(StockpileManager_EmuSide.getDefaultPartial());
                rtcSpecTemplate.Insert(Render.getDefaultPartial());

                AllSpec.CorruptCoreSpec = new FullSpec(rtcSpecTemplate, !RtcCore.Attached); //You have to feed a partial spec as a template

                AllSpec.CorruptCoreSpec.SpecUpdated += (o, e) =>
                {
                    PartialSpec partial = e.partialSpec;
                    if (IsStandaloneUI)
                    {
                        LocalNetCoreRouter.Route(NetcoreCommands.CORRUPTCORE, NetcoreCommands.REMOTE_PUSHCORRUPTCORESPECUPDATE, partial, true);
                    }
                    else
                    {
                        LocalNetCoreRouter.Route(NetcoreCommands.UI, NetcoreCommands.REMOTE_PUSHCORRUPTCORESPECUPDATE, partial, true);
                    }
                };

                /*
                if (RTC_StockpileManager.BackupedState != null)
                    RTC_StockpileManager.BackupedState.Run();
                else
                    CorruptCoreSpec.Update(RTCSPEC.CORE_AUTOCORRUPT.ToString(), false);
                    */
            }
            catch (Exception ex)
            {
                if (CloudDebug.ShowErrorDialog(ex, true) == DialogResult.Abort)
                {
                    throw new AbortEverythingException();
                }
            }
        }
        public static void LoadPlugins(string[] paths = null)
        {
            if (paths == null)
                paths = new[] { PluginDir };

            RTCSide side = RTCSide.Server;
            if (IsEmulatorSide)
                side = RTCSide.Client;
            if (Attached)
            {
                side = RTCSide.Both;
            }
            pluginHost.Start(paths, side);
        }

        public static PartialSpec getDefaultPartial()
        {
            try
            {
                var partial = new PartialSpec("RTCSpec");

                partial[RTCSPEC.CORE_SELECTEDENGINE] = CorruptionEngine.NIGHTMARE;

                partial[RTCSPEC.CORE_CURRENTPRECISION] = 1;
                partial[RTCSPEC.CORE_CURRENTALIGNMENT] = 0;
                partial[RTCSPEC.CORE_INTENSITY] = 1L;
                partial[RTCSPEC.CORE_ERRORDELAY] = 1L;
                partial[RTCSPEC.CORE_RADIUS] = "SPREAD";

                partial[RTCSPEC.CORE_EXTRACTBLASTLAYER] = false;
                partial[RTCSPEC.CORE_AUTOCORRUPT] = false;

                partial[RTCSPEC.CORE_EMULATOROSDDISABLED] = true;
                partial[RTCSPEC.CORE_DONTCLEANSAVESTATESONQUIT] = false;
                partial[RTCSPEC.CORE_SHOWCONSOLE] = false;

                if (Params.IsParamSet("ALLOW_CROSS_CORE_CORRUPTION"))
                {
                    partial[RTCSPEC.CORE_ALLOWCROSSCORECORRUPTION] = (string.Equals(Params.ReadParam("ALLOW_CROSS_CORE_CORRUPTION"), "TRUE", StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    partial[RTCSPEC.CORE_ALLOWCROSSCORECORRUPTION] = false;
                }

                if (Params.IsParamSet("REROLL_SOURCEADDRESS"))
                {
                    partial[RTCSPEC.CORE_REROLLSOURCEADDRESS] = (string.Equals(Params.ReadParam("REROLL_SOURCEADDRESS"), "TRUE", StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    partial[RTCSPEC.CORE_REROLLSOURCEADDRESS] = false;
                }

                if (Params.IsParamSet("REROLL_SOURCEDOMAIN"))
                {
                    partial[RTCSPEC.CORE_REROLLSOURCEDOMAIN] = (string.Equals(Params.ReadParam("REROLL_SOURCEDOMAIN"), "TRUE", StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    partial[RTCSPEC.CORE_REROLLSOURCEDOMAIN] = false;
                }

                if (Params.IsParamSet("REROLL_ADDRESS"))
                {
                    partial[RTCSPEC.CORE_REROLLADDRESS] = (string.Equals(Params.ReadParam("REROLL_ADDRESS"), "TRUE", StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    partial[RTCSPEC.CORE_REROLLADDRESS] = false;
                }

                if (Params.IsParamSet("REROLL_DOMAIN"))
                {
                    partial[RTCSPEC.CORE_REROLLDOMAIN] = (string.Equals(Params.ReadParam("REROLL_DOMAIN"), "TRUE", StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    partial[RTCSPEC.CORE_REROLLDOMAIN] = false;
                }

                if (Params.IsParamSet("REROLL_FOLLOWSCUSTOMENGINE"))
                {
                    partial[RTCSPEC.CORE_REROLLFOLLOWENGINESETTINGS] = (string.Equals(Params.ReadParam("REROLL_FOLLOWSCUSTOMENGINE"), "TRUE", StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    partial[RTCSPEC.CORE_REROLLFOLLOWENGINESETTINGS] = false;
                }

                if (Params.IsParamSet("REROLL_USESVALUELIST"))
                {
                    partial[RTCSPEC.CORE_REROLLIGNOREORIGINALSOURCE] = (string.Equals(Params.ReadParam("REROLL_USESVALUELIST"), "TRUE", StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    partial[RTCSPEC.CORE_REROLLIGNOREORIGINALSOURCE] = false;
                }

                return partial;
            }
            catch (Exception ex)
            {
                if (CloudDebug.ShowErrorDialog(ex) == DialogResult.Abort)
                {
                    throw new AbortEverythingException();
                }

                return null;
            }
        }

        public static void DownloadProblematicProcesses()
        {
            //Windows does the big dumb: part 11
            WebRequest.DefaultWebProxy = null;

            //Do this on its own thread as downloading the json is slow
            (new Thread(() =>
            {
                var localPath = Path.Combine(Params.ParamsDir, "BADPROCESSES");

                var json = "";
                try
                {
                    if (File.Exists(localPath))
                    {
                        DateTime lastModified = File.GetLastWriteTime(localPath);
                        if (lastModified.Date == DateTime.Today)
                        {
                            ProblematicProcesses = JsonConvert.DeserializeObject<List<ProblematicProcess>>(File.ReadAllText(localPath));
                            CheckForProblematicProcesses();
                            return;
                        }
                    }

                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromMilliseconds(5000);
                        //Using .Result makes it synchronous
                        json = client.GetStringAsync("http://redscientist.com/software/rtc/ProblematicProcesses.json")
                            .Result;
                    }

                    File.WriteAllText(localPath, json);
                }
                catch (Exception ex)
                {
                    if (ex is WebException)
                    {
                        //Couldn't download the new one so just fall back to the old one if it's there
                        logger.Error(ex, "Failed to download ProblematicProcesses");
                        if (File.Exists(localPath))
                        {
                            try
                            {
                                json = File.ReadAllText(localPath);
                            }
                            catch (Exception _ex)
                            {
                                logger.Error(_ex, "Couldn't read BADPROCESSES");
                                return;
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        logger.Error(ex, "Unknown exception when downloading ProblematicProcesses");
                    }
                }

                try
                {
                    ProblematicProcesses = JsonConvert.DeserializeObject<List<ProblematicProcess>>(json);
                    CheckForProblematicProcesses();
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                    if (File.Exists(localPath))
                    {
                        File.Delete(localPath);
                    }

                    throw;
                }
            })).Start();
        }

        //Checks if any problematic processes are found
        public static bool Warned = false;
        public static void CheckForProblematicProcesses()
        {
            logger.Info("Entering CheckForProblematicProcesses");
            if (Warned || ProblematicProcesses == null)
            {
                return;
            }

            try
            {
                var processes = Process.GetProcesses().Select(it => $"{it.ProcessName.ToUpper()}").OrderBy(x => x)
                    .ToArray();

                //Warn based on loaded processes
                foreach (var item in ProblematicProcesses)
                {
                    if (processes.Contains(item.Name.ToUpper()))
                    {
                        MessageBox.Show(item.Message, "Incompatible Program Detected!");
                        Warned = true;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                if (CloudDebug.ShowErrorDialog(ex, true) == DialogResult.Abort)
                {
                    throw new AbortEverythingException();
                }

                return;
            }
            finally
            {
                logger.Info("Exiting CheckForProblematicProcesses");
            }
        }
        public static BlastLayer GenerateBlastLayerOnAllThreads()
        {
            //We pull the domains here because if the syncsettings changed, there's a chance the domains changed
            var domains = (string[])AllSpec.UISpec["SELECTEDDOMAINS"];
            var cpus = Environment.ProcessorCount;

            //If there is only one thread, only generate a single BlastLayer.
            if (cpus == 1 || AllSpec.VanguardSpec[VSPEC.SUPPORTS_MULTITHREAD] == null)
            {
                return GenerateBlastLayer(domains);
            }

            //if emulator supports multithreaded access of the domains, disregard the emulation thread and just span threads...
            var reminder = Intensity % (cpus - 1);
            var splitintensity = (Intensity - reminder) / (cpus - 1);
            var tasks = new Task<BlastLayer>[cpus];
            for (var i = 0; i < cpus; i++)
            {
                var requestedIntensity = splitintensity;
                if (i == 0 && reminder != 0)
                {
                    requestedIntensity = reminder;
                }

                tasks[i] = Task.Factory.StartNew(() => GenerateBlastLayer(domains, requestedIntensity));
            }

            Task.WaitAll(tasks);

            BlastLayer bl = tasks[0].Result ?? new BlastLayer();

            for (var i = 1; i < tasks.Length; i++)
            {
                if (tasks[i].Result != null)
                {
                    bl.Layer.AddRange(tasks[i].Result.Layer);
                }
            }

            if (bl.Layer.Count == 0)
            {
                bl = null;
            }

            return bl;
        }

        private static long GetIntensity(long overrideIntensity)
        {
            var intensity = RtcCore.Intensity; //general RTC intensity

            if (overrideIntensity != -1)
            {
                intensity = overrideIntensity;
            }

            // Capping intensity at engine-specific maximums
            if ((RtcCore.SelectedEngine == CorruptionEngine.HELLGENIE ||
                RtcCore.SelectedEngine == CorruptionEngine.FREEZE ||
                RtcCore.SelectedEngine == CorruptionEngine.PIPE ||
                (RtcCore.SelectedEngine == CorruptionEngine.CUSTOM && RTC_CustomEngine.Lifetime == 0)) &&
                intensity > StepActions.MaxInfiniteBlastUnits)
            {
                intensity = StepActions.MaxInfiniteBlastUnits; //Capping for cheat max
            }

            return intensity;
        }

        //Generates or applies a blast layer using one of the multiple BlastRadius algorithms

        public static BlastLayer GenerateBlastLayer(string[] selectedDomains, long overrideIntensity = -1)
        {
            if (overrideIntensity == 0)
            {
                return null;
            }

            try
            {
                try
                {
                    if (RtcCore.SelectedEngine == CorruptionEngine.BLASTGENERATORENGINE)
                    {
                        //It will query a BlastLayer generated by the Blast Generator
                        var blastLayer = RTC_BlastGeneratorEngine.GetBlastLayer();
                        if (blastLayer == null)
                        {
                            //We return an empty blastlayer so when it goes to apply it, it doesn't find a null blastlayer and try and apply to the domains which aren't enabled resulting in an exception
                            return new BlastLayer();
                        }

                        return blastLayer;
                    }

                    if (selectedDomains == null || selectedDomains.Length == 0)
                    {
                        return null;
                    }

                    //Spec lookups add up really fast if you have a high intensity so we cache stuff we're going to be looking up over and over again
                    CachedPrecision = CurrentPrecision;
                    var cachedDomainSizes = new long[selectedDomains.Length];
                    for (var i = 0; i < selectedDomains.Length; i++)
                    {
                        cachedDomainSizes[i] = MemoryDomains.GetInterface(selectedDomains[i]).Size;
                    }

                    var blastInfo = new BlastInfo {
                        alignment = Alignment,
                        domainSizes = cachedDomainSizes,
                        engine = SelectedEngine,
                        intensity = GetIntensity(overrideIntensity),
                        precision = CachedPrecision,
                        selectedDomains = selectedDomains
                    };

                    var bl = RtcCore.Radius(blastInfo);
                    if (bl.Layer.Count == 0)
                    {
                        return null;
                    }

                    return bl;
                }
                catch (Exception ex)
                {
                    var additionalInfo = "";

                    // if (MemoryDomains.GetInterface(domain) == null)
                    // {
                    //     additionalInfo = "Unable to get an interface to the selected memory domain! \nTry clicking the Auto-Select Domains button to refresh the domains!\n\n";
                    // }

                    throw new Exception(additionalInfo + ex.Message);
                }
            }
            catch (Exception ex)
            {
                var dr = CloudDebug.ShowErrorDialog(ex, true);

                if (RtcCore.AutoCorrupt)
                {
                    RtcCore.AutoCorrupt = false;
                    LocalNetCoreRouter.Route(NetcoreCommands.UI, NetcoreCommands.ERROR_DISABLE_AUTOCORRUPT);
                }

                if (dr == DialogResult.Abort)
                {
                    throw new AbortEverythingException();
                }

                return null;
            }
        }

        public static BlastTarget GetBlastTarget()
        {
            //Standalone version of BlastRadius SPREAD
            var selectedDomains = (string[])AllSpec.UISpec["SELECTEDDOMAINS"];
            var domain = selectedDomains[RtcCore.RND.Next(selectedDomains.Length)];
            var maxAddress = MemoryDomains.GetInterface(domain).Size;
            var randomAddress = RtcCore.RND.NextLong(0, maxAddress - 1);

            return new BlastTarget(domain, randomAddress);
        }

        public static string GetRandomKey()
        {
            //Generates unique string ids that are human-readable, unlike GUIDs
            var key = RtcCore.RND.Next(1, 9999).ToString() + RtcCore.RND.Next(1, 9999).ToString() + RtcCore.RND.Next(1, 9999).ToString() + RtcCore.RND.Next(1, 9999).ToString();
            return key;
        }

        public static void GenerateAndBlast()
        {
            BlastLayer bl = null;
            void _generateAndBlast()
            {
                bl = GenerateBlastLayerOnAllThreads();
                bl?.Apply(false, true);
            }
            //If the emulator uses callbacks, we do everything on the main thread and once we're done, we unpause emulation
            if ((bool?)AllSpec.VanguardSpec[VSPEC.LOADSTATE_USES_CALLBACKS] ?? false)
            {
                SyncObjectSingleton.FormExecute(_generateAndBlast);
            }
            else //We can just do everything on the emulation thread as it'll block
            {
                SyncObjectSingleton.EmuThreadExecute(_generateAndBlast, true);
            }
        }

        /*
        public static void ApplyBlastLayer(BlastLayer bl)
        {
            if(bl.Layer != null)
                bl.Apply();
        }*/

        public static void OnProgressBarUpdate(object sender, ProgressBarEventArgs e)
        {
            ProgressBarHandler?.Invoke(sender, e);
        }

        public static void LOAD_GAME_DONE()
        {
            LoadGameDone?.Invoke(null, null);
        }

        public static void GAME_CLOSED(bool fullyClosed = false)
        {
            GameClosed?.Invoke(null, new GameClosedEventArgs(fullyClosed));
        }

        public static void KILL_HEX_EDITOR()
        {
        }
    }

    public static class RtcClock
    {
        static int CPU_STEP_Count = 0;

        public static void STEP_CORRUPT(bool executeActions, bool performStep)
        {
            if (executeActions)
            {
                StepActions.Execute();
            }

            if (performStep)
            {
                CPU_STEP_Count++;

                var autoCorrupt = RtcCore.AutoCorrupt;
                var errorDelay = RtcCore.ErrorDelay;
                if (autoCorrupt && CPU_STEP_Count >= errorDelay)
                {
                    CPU_STEP_Count = 0;
                    BlastLayer bl = RtcCore.GenerateBlastLayer((string[])AllSpec.UISpec["SELECTEDDOMAINS"]);
                    bl?.Apply(false, false);
                }
            }
        }

        public static void RESET_COUNT()
        {
            CPU_STEP_Count = 0;
        }
    }
}
