﻿using ABI_RC.VideoPlayer.Scripts;
using BTKUILib;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Reflection;
using BTKUILib.UIObjects;
using BTKUILib.UIObjects.Components;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using Semver;
using ABI.CCK.Components;
using ABI_RC.Core.InteractionSystem;
using HarmonyLib;
using System.Net;
using System.Web;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ABI_RC.Core.UI;


namespace VideoRemote
{
    public static class ModBuildInfo
    {
        public const string Name = "Video Remote";
        public const string Author = "Shin, Nirvash";
        public const string Version = "1.2.0";
        public const string Description = "This allows you to use the video player with the menu.";
        public const string DownloadLink = "https://github.com/DjShinter/VideoRemote/releases";
    }
    public sealed class VideoRemoteMod : MelonMod
    {
        public static VideoRemoteMod Instance;

        public static MelonPreferences_Category cat;
        private const string catagory = "VideoRemoteMod";
        public static MelonPreferences_Entry<bool> sponsorSkip_sponsor;
        public static MelonPreferences_Entry<bool> sponsorSkip_selfpromo;
        public static MelonPreferences_Entry<bool> sponsorSkip_interaction;
        public static MelonPreferences_Entry<bool> sponsorSkip_intro;

        private static Category VideoPlayerListMain, VideoPlayerList, AdvancedOptions, videoName;
        private static Page AdvOptionsPage, SponsorSkipEvents, savedURLsPage;
        private static SliderFloat volumeSilder;
        private static string VideoFolderString, MainPageString, SponsorSkipEventsString, AdvOptionsString, savedURLsPageString;
        private static string lastQMPage = "";

        public static bool _initalized = new();
        private static ViewManagerVideoPlayer VideoPlayerSelected = new();
        private const string FolderRoot = "UserData/VideoRemote/";
        private const string FolderConfig = "savedURLsList.txt";
        public static Dictionary<string, (string, DateTime)> savedURLs = new Dictionary<string, (string, DateTime)>(); //URL,(Name,Date)

        private static GameObject localScreen;
        private static bool pickupable = false;
        private static float sizeScale = 1;

        private static bool sponsorSkip = false;
        private static object sponsorSkipCheckCoroutine, sponsorSkipLiveCoroutine;
        private static string lastvideo = "zzzzzzzzzzzzzzzzzzzzz09";
        private static string sponsorskipVideo = "xxxxxxxxxxxxxxxxxxxx08";
        private static SponsorSkipSegment[] sponsorskipResult;
        private static List<(string, float[])> sponsorskips = new List<(string, float[])>();
        private static float[] lastskip = new float[2];

        private static bool TryingToReplay = false;

        public override void OnInitializeMelon()
        {
            Instance = this;

            cat = MelonPreferences.CreateCategory(catagory, "Video Remote", true);
            sponsorSkip_sponsor = MelonPreferences.CreateEntry(catagory, nameof(sponsorSkip_sponsor), true, "Skips Segment Sponsor");
            sponsorSkip_selfpromo = MelonPreferences.CreateEntry(catagory, nameof(sponsorSkip_selfpromo), false, "Skips Segment selfpromo");
            sponsorSkip_interaction = MelonPreferences.CreateEntry(catagory, nameof(sponsorSkip_interaction), false, "Skips Segment interaction");
            sponsorSkip_intro = MelonPreferences.CreateEntry(catagory, nameof(sponsorSkip_intro), false, "Skips Segment intro");

            if (!RegisteredMelons.Any(x => x.Info.Name.Equals("BTKUILib") && x.Info.SemanticVersion != null && x.Info.SemanticVersion.CompareTo(new SemVersion(1)) >= 0))
            {
                MelonLogger.Error("BTKUILib was not detected or it is outdated! VideoRemote cannot function without it!");
                MelonLogger.Error("Please download an updated copy for BTKUILib!");
                return;
            }
            _initalized = false;
            VideoPlayerSelected = null;

            if (!Directory.Exists(FolderRoot))
            {
                Directory.CreateDirectory(FolderRoot);
            }
            LoadURLs();
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (SceneManager.GetActiveScene().name == "Init")
            {
                if (!_initalized)
                {
                    _initalized = true;
                    SetupIcons();
                    HarmonyInstance.Patch(typeof(CVR_MenuManager).GetMethod(nameof(CVR_MenuManager.ToggleQuickMenu)), null, new HarmonyMethod(typeof(VideoRemoteMod).GetMethod(nameof(QMtoggle), BindingFlags.NonPublic | BindingFlags.Static)));
                }
            }
            VideoPlayerSelected = null;
        }

        private void SetupIcons()
        {
            QuickMenuAPI.PrepareIcon("VideoRemoteMod", "VideoPlayerModLogo", Assembly.GetExecutingAssembly().GetManifestResourceStream("VideoRemote.UI.Images.VideoPlayerModLogo.png"));
            QuickMenuAPI.PrepareIcon("VideoRemoteMod", "VideoPlayerModPlay2", Assembly.GetExecutingAssembly().GetManifestResourceStream("VideoRemote.UI.Images.Play.png"));
            QuickMenuAPI.PrepareIcon("VideoRemoteMod", "VideoPlayerModPause2", Assembly.GetExecutingAssembly().GetManifestResourceStream("VideoRemote.UI.Images.Pause.png"));
            QuickMenuAPI.PrepareIcon("VideoRemoteMod", "VideoPlayerModNewScreen", Assembly.GetExecutingAssembly().GetManifestResourceStream("VideoRemote.UI.Images.NewScreen.png"));
            QuickMenuAPI.PrepareIcon("VideoRemoteMod", "VideoPlayerModWhite-Minus", Assembly.GetExecutingAssembly().GetManifestResourceStream("VideoRemote.UI.Images.White-Minus.png"));
            QuickMenuAPI.PrepareIcon("VideoRemoteMod", "VideoPlayerModWhite-Plus", Assembly.GetExecutingAssembly().GetManifestResourceStream("VideoRemote.UI.Images.White-Plus.png"));
            QuickMenuAPI.PrepareIcon("VideoRemoteMod", "VideoPlayerModSound", Assembly.GetExecutingAssembly().GetManifestResourceStream("VideoRemote.UI.Images.Sound.png"));
            QuickMenuAPI.PrepareIcon("VideoRemoteMod", "VideoPlayerModKeys", Assembly.GetExecutingAssembly().GetManifestResourceStream("VideoRemote.UI.Images.Keys.png"));
            QuickMenuAPI.PrepareIcon("VideoRemoteMod", "VideoPlayerModVideoPlayer", Assembly.GetExecutingAssembly().GetManifestResourceStream("VideoRemote.UI.Images.VideoPlayer.png"));
            QuickMenuAPI.PrepareIcon("VideoRemoteMod", "VideoPlayerModPastePlay", Assembly.GetExecutingAssembly().GetManifestResourceStream("VideoRemote.UI.Images.PastePlay.png"));
            QuickMenuAPI.PrepareIcon("VideoRemoteMod", "VideoPlayerModSave", Assembly.GetExecutingAssembly().GetManifestResourceStream("VideoRemote.UI.Images.Save.png"));
            QuickMenuAPI.PrepareIcon("VideoRemoteMod", "VideoPlayerModSponsorSkip", Assembly.GetExecutingAssembly().GetManifestResourceStream("VideoRemote.UI.Images.SponsorSkip.png"));
            QuickMenuAPI.PrepareIcon("VideoRemoteMod", "VideoPlayerModArrow-Left", Assembly.GetExecutingAssembly().GetManifestResourceStream("VideoRemote.UI.Images.Arrow-Left.png"));
            QuickMenuAPI.PrepareIcon("VideoRemoteMod", "VideoPlayerModArrow-Right", Assembly.GetExecutingAssembly().GetManifestResourceStream("VideoRemote.UI.Images.Arrow-Right.png"));
            QuickMenuAPI.PrepareIcon("VideoRemoteMod", "VideoPlayerModAdvSettings2", Assembly.GetExecutingAssembly().GetManifestResourceStream("VideoRemote.UI.Images.AdvSettings.png"));
            QuickMenuAPI.PrepareIcon("VideoRemoteMod", "VideoPlayerModReloadVideo", Assembly.GetExecutingAssembly().GetManifestResourceStream("VideoRemote.UI.Images.ReloadVideo.png"));
            QuickMenuAPI.PrepareIcon("VideoRemoteMod", "VideoPlayerModLoadURLs", Assembly.GetExecutingAssembly().GetManifestResourceStream("VideoRemote.UI.Images.LoadURLs.png"));

            SetupUI();
            QuickMenuAPI.OnOpenedPage += OnPageOpen;
            QuickMenuAPI.OnBackAction += OnPageBack;

            static void SetupUI()
            {
                var CustomPage = new Page("VideoRemoteMod", "VideoRemotePage", true, "VideoPlayerModLogo")
                {
                    //This sets the title that appears at the very top in the header bar
                    MenuTitle = ModBuildInfo.Name,
                    MenuSubtitle = ModBuildInfo.Description
                };
                MainPageString = CustomPage.ElementID;

                videoName = CustomPage.AddCategory("");
                var category = videoName;
                var Folder = category.AddPage("Select Video Player", "VideoPlayerModLogo", "List Video Players in the World", "VideoRemoteMod");
                VideoFolderString = Folder.ElementID;
                VideoPlayerListMain = Folder.AddCategory("");
                VideoPlayerList = Folder.AddCategory("Video Players In World");
                
                var button = category.AddButton("Play Video", "VideoPlayerModPlay2", "Play the Video");
                button.OnPress += () =>
                {

                    if (VideoPlayerSelected != null)
                    {
                        VideoPlayerSelected.Play();
                    }
                    else
                    {
                        QuickMenuAPI.ShowAlertToast("Video Player Not Selected or does not exist.", 2);
                        MelonLogger.Msg("Video Player Not Selected or does not exist.");
                    }

                };
                var button2 = category.AddButton("Pause Video", "VideoPlayerModPause2", "Pause the Video");
                button2.OnPress += () =>
                {
                    if (VideoPlayerSelected != null)
                    {
                        VideoPlayerSelected.Pause();
                    }
                    else
                    {
                        QuickMenuAPI.ShowAlertToast("Video Player Not Selected or does not exist.", 2);
                        MelonLogger.Msg("Video Player Not Selected or does not exist.");
                    }

                };
                var button3 = category.AddButton("Paste and Play Video", "VideoPlayerModPastePlay", "Paste and Play the Video");
                button3.OnPress += () =>
                {
                    if (VideoPlayerSelected != null)
                    {
                        QuickMenuAPI.ShowConfirm("Confirm", "Paste and Play Video?", () =>
                        {
                            VideoPlayerSelected.PasteAndPlay();
                            MelonCoroutines.Start(Instance.SetCurrentVideoNameDelay());
                        }, () => { }, "Yes", "No");
                    }
                    else
                    {
                        QuickMenuAPI.ShowAlertToast("Video Player Not Selected or does not exist.", 2);
                        MelonLogger.Msg("Video Player Not Selected or does not exist.");
                    }

                };

                volumeSilder = CustomPage.AddSlider("Volume", "Video Player Volume", .5f, 0f, 1f);
                volumeSilder.OnValueUpdated += action =>
                {
                    if (VideoPlayerSelected != null)
                    {
                        VideoPlayerSelected.SetLocalAudioVolume(volumeSilder.SliderValue);
                    }
                };

                AdvancedOptions = CustomPage.AddCategory("");
                PopulateAdvancedButtons();

                var catLocalVid = CustomPage.AddCategory("Local Video Player Screen");
                var buttSpawnScreen = catLocalVid.AddButton("Spawn/Toggle Local Screen", "VideoPlayerModNewScreen", "Creates a local copy of the video player screen in front of you.<p>You must select a video player first.").OnPress += () =>
                {
                    if (VideoPlayerSelected != null || (!localScreen?.Equals(null) ?? false))
                    {
                        ToggleLocalScreen();
                    }
                    else
                    {
                        QuickMenuAPI.ShowAlertToast("Can not create local screen. Video Player Not Selected or does not exist.", 3);
                        MelonLogger.Msg("Can not create local screen. Video Player Not Selected or does not exist.");
                    }
                };
                catLocalVid.AddButton("Smaller", "VideoPlayerModWhite-Minus", "Decreases the screen size").OnPress += () =>
                {
                    if (sizeScale > .15) sizeScale -= .15f;
                    UpdateLocalScreen();
                };
                catLocalVid.AddButton("Larger", "VideoPlayerModWhite-Plus", "Increases the screen size").OnPress += () =>
                {
                    sizeScale += .15f;
                    UpdateLocalScreen();
                };
                catLocalVid.AddToggle("Pickupable", "Toggles pickup of the local screen", pickupable).OnValueUpdated += action =>
                {
                    pickupable = action;
                    UpdateLocalScreen();
                };
            }
        }


        private static void SaveUrl(ViewManagerVideoPlayer vp)
        {
            var dateFormatOut = "yyyy'-'MM'-'dd";
            if (!String.IsNullOrWhiteSpace(vp.videoUrl.text))
            {
                string vidname = Utils.VideoNameFormat(vp);
                if (!File.Exists(FolderRoot + FolderConfig))
                {
                    using StreamWriter sw = File.CreateText(FolderRoot + FolderConfig);
                    sw.WriteLine(DateTime.Now.ToString(dateFormatOut) + " ␟ " + vidname + " ␟ " + vp.videoUrl.text);
                }
                else
                {
                    if (File.Exists(FolderRoot + FolderConfig))
                    {
                        using StreamWriter sw = File.AppendText(FolderRoot + FolderConfig);
                        sw.WriteLine(DateTime.Now.ToString(dateFormatOut) + " ␟ " + vidname + " ␟ " + vp.videoUrl.text);
                    }
                }
                if(!savedURLs.ContainsKey(vp.videoUrl.text)) savedURLs.Add(vp.videoUrl.text, (vidname, DateTime.Now));
            }
            else
            {
                MelonLogger.Msg("There was nothing to save.");
            }
        }

        public static void LoadURLs()
        {
            var dateFormatIn = "yyyy-MM-dd";
            if (File.Exists(FolderRoot + FolderConfig))
            {
                try
                {
                    savedURLs = new Dictionary<string, (string, DateTime)>(File.ReadAllLines(FolderRoot + FolderConfig).Select(s => s.Split(new char[] { '␟' }, StringSplitOptions.RemoveEmptyEntries))
                      .Where(x => x.Length == 3).ToLookup(p => p[2].Trim(), p => (p[1].Trim(), Utils.ParseDate(p[0].Trim(), dateFormatIn)))
                      .ToDictionary(p => p.Key, p => p.First()));
                }
                catch (Exception ex) { MelonLogger.Warning("Error reading previously saved URLs \n" + ex.ToString()); }
            }
        }

        private static void PopulateVideoList()
        {
            if (VideoPlayerListMain.IsGenerated) VideoPlayerListMain.ClearChildren();
            if (VideoPlayerList.IsGenerated) VideoPlayerList.ClearChildren();

            VideoPlayerListMain.AddButton("Load Video Players", "VideoPlayerModLogo", "Reload the list of Video Players in the world").OnPress += () =>
            {
                PopulateVideoList();
            };

            if (GameObject.FindObjectOfType<CVRVideoPlayer>())
            {
                foreach (CVRVideoPlayer CVRvp in GameObject.FindObjectsOfType<CVRVideoPlayer>())
                {
                    List<IVideoPlayerUi> savedvpui = Traverse.Create(CVRvp).Field("VideoPlayerUis").GetValue<List<IVideoPlayerUi>>();
                    foreach (ViewManagerVideoPlayer vp in savedvpui.Cast<ViewManagerVideoPlayer>())
                    {
                        var dist = Math.Abs(Vector3.Distance(CVRvp.gameObject.transform.position, Camera.main.transform.position)).ToString("F2").TrimEnd('0');
                        var button = VideoPlayerList.AddButton("Video Player \n (Hover)", "VideoPlayerModVideoPlayer", $"Video:{Utils.VideoNameFormat(vp)}, Distance:{dist}<p>{Utils.GetPath(CVRvp.gameObject.transform)}");// | {CVRvp.playerId}");
                        button.OnPress += () =>
                        {
                            VideoPlayerSelected = vp;
                            MelonLogger.Msg(CVRvp.playerId + " has been selected");
                        };
                    }
                }
            }
        }

        private static void PopulateAdvancedButtons()
        { //Doing this way so the button states always show the current value from the video player. This is a lot of hacky stuff to avoid using Bono's 'hack' for non-root pages.
            if (AdvancedOptions.IsGenerated) AdvancedOptions.ClearChildren(); 
            if (AdvOptionsPage != null && AdvOptionsPage.IsGenerated) AdvOptionsPage.ClearChildren();
            if (SponsorSkipEvents != null && SponsorSkipEvents.IsGenerated) SponsorSkipEvents.ClearChildren();
            if (savedURLsPage != null && savedURLsPage.IsGenerated) savedURLsPage.ClearChildren();

            AdvOptionsPage = AdvancedOptions.AddPage("VideoPlayer Options", "VideoPlayerModAdvSettings2", "Set Permissions, Network Sync, Audio Mode, Reload current video", "VideoRemoteMod");
            AdvOptionsString = AdvOptionsPage.ElementID;
            {
                var advSubPageCat = AdvOptionsPage.AddCategory("");
                {
                    var current = (VideoPlayerSelected != null) ? VideoPlayerSelected.videoPlayer.ControlPermission.ToString() : "None Selected";
                    var butt = advSubPageCat.AddButton($"Permission: {current}", "VideoPlayerModKeys", $"Toggles the video player permission between Everyone and InstanceModerators");
                    butt.OnPress += () =>
                    {
                        if (VideoPlayerSelected != null)
                        {
                            VideoPlayerUtils.ControlPermission setTo = VideoPlayerUtils.ControlPermission.Everyone;
                            if (VideoPlayerSelected.videoPlayer.ControlPermission == VideoPlayerUtils.ControlPermission.Everyone)
                            {
                                QuickMenuAPI.ShowConfirm("Confirm", "Switch permission to InstanceModerators?", () =>
                                {
                                    setTo = VideoPlayerUtils.ControlPermission.InstanceModerators;
                                    VideoPlayerSelected.videoPlayer.SetControlPermission(setTo);
                                    butt.ButtonText = $"Permission: {setTo}";
                                }, () => { }, "Yes", "No");

                            }
                            else
                            {
                                setTo = VideoPlayerUtils.ControlPermission.Everyone;
                                VideoPlayerSelected.videoPlayer.SetControlPermission(setTo);
                                butt.ButtonText = $"Permission: {setTo}";
                            }


                        }
                        else
                        {
                            QuickMenuAPI.ShowAlertToast("Video Player Not Selected or does not exist.", 2);
                            MelonLogger.Msg("Video Player Not Selected or does not exist.");
                        }
                    };
                }
                {
                    var butt = advSubPageCat.AddToggle("Network Sync", "Toggles Network Sync to state", true);
                    butt.OnValueUpdated += action =>
                    {
                        if (VideoPlayerSelected != null)
                        {
                            VideoPlayerSelected.videoPlayer.SetNetworkSync(action);
                        }
                        else
                        {
                            QuickMenuAPI.ShowAlertToast("Video Player Not Selected or does not exist.", 3);
                            MelonLogger.Msg("Video Player Not Selected or does not exist.");
                        }
                    };
                }
                {
                    var current = (VideoPlayerSelected != null) ? VideoPlayerSelected.videoPlayer.audioPlaybackMode.ToString() : "None Selected";
                    var butt = advSubPageCat.AddButton($"Audio Mode: {current}", "VideoPlayerModSound", $"Toggles audio mode between Direct, AudioSource, RoomScale<p>Currently: {current}");
                    butt.OnPress += () =>
                    {
                        if (VideoPlayerSelected != null)
                        {
                            try
                            {
                                switch (VideoPlayerSelected.videoPlayer.audioPlaybackMode)
                                {
                                    case VideoPlayerUtils.AudioMode.Direct: VideoPlayerSelected.videoPlayer.SetAudioMode(VideoPlayerUtils.AudioMode.AudioSource); break;
                                    case VideoPlayerUtils.AudioMode.AudioSource: VideoPlayerSelected.videoPlayer.SetAudioMode(VideoPlayerUtils.AudioMode.RoomScale); break;
                                    case VideoPlayerUtils.AudioMode.RoomScale: VideoPlayerSelected.videoPlayer.SetAudioMode(VideoPlayerUtils.AudioMode.Direct); break;
                                    default: VideoPlayerSelected.videoPlayer.SetAudioMode(VideoPlayerUtils.AudioMode.AudioSource); break;
                                }
                                butt.ButtonText = $"Audio Mode: {VideoPlayerSelected.videoPlayer.audioPlaybackMode}";
                                butt.ButtonTooltip = $"Toggles audio mode between Direct, AudioSource, RoomScale<p>Currently: {VideoPlayerSelected.videoPlayer.audioPlaybackMode}";
                            }
                            catch (System.Exception ex)
                            {
                                MelonLogger.Error($"Error when changing video audio source\n" + ex.ToString());
                                butt.ButtonText = $"Audio Mode: Error";
                                QuickMenuAPI.ShowAlertToast("Error when changing video audio source.", 3);
                            }
                        }
                        else
                        {
                            QuickMenuAPI.ShowAlertToast("Video Player Not Selected or does not exist.", 2);
                            MelonLogger.Msg("Video Player Not Selected or does not exist.");
                        }
                    };
                }
                {
                    var butt = advSubPageCat.AddButton($"Reload video at timestamp", "VideoPlayerModReloadVideo", $"Reloads the current video and sets it back to the current timestamp");
                    butt.OnPress += () =>
                    {
                        if (VideoPlayerSelected != null)
                        {
                            if (TryingToReplay)
                                QuickMenuAPI.ShowAlertToast("Video reload already running", 2);
                            else
                                MelonCoroutines.Start(Instance.ReloadVideo());
                        }
                        else
                        {
                            QuickMenuAPI.ShowAlertToast("Video Player Not Selected or does not exist.", 2);
                            MelonLogger.Msg("Video Player Not Selected or does not exist.");
                        }
                    };
                }
            }
            //
            SponsorSkipEvents = AdvancedOptions.AddPage("SponsorBlock", "VideoPlayerModSponsorSkip", "SponsorBlock can automatically skip sponsor segments in Youtube videos.", "VideoRemoteMod");
            SponsorSkipEventsString = SponsorSkipEvents.ElementID;
            {
                var skipHeaderCat = SponsorSkipEvents.AddCategory($"");
                skipHeaderCat.AddToggle("Enable", "Enable SponsorBlock", sponsorSkip).OnValueUpdated += action =>
                {
                    sponsorSkip = action;
                    StartSponsorSkip();
                };

                var skipSettings = SponsorSkipEvents.AddCategory($"Segments types to skip");
                skipSettings.AddToggle("Sponsor", "Skip sponsor segments.<p>Doesn't skip if <5 seconds.", sponsorSkip_sponsor.Value).OnValueUpdated += action =>
                {
                    sponsorSkip_sponsor.Value = action;

                    if (VideoPlayerSelected?.videoPlayer?.VideoPlayer != null && VideoPlayerSelected.videoPlayer.lastNetworkVideoUrl == sponsorskipVideo)
                        PrepSkipList();
                };
                skipSettings.AddToggle("Self Promo", "Skip self promo segments.<p>Doesn't skip if <5 seconds.", sponsorSkip_selfpromo.Value).OnValueUpdated += action =>
                {
                    sponsorSkip_selfpromo.Value = action;
                    if (VideoPlayerSelected?.videoPlayer?.VideoPlayer != null && VideoPlayerSelected.videoPlayer.lastNetworkVideoUrl == sponsorskipVideo)
                        PrepSkipList();
                };
                skipSettings.AddToggle("Interaction", "Skip interaction segments.<p>Doesn't skip if <5 seconds.", sponsorSkip_interaction.Value).OnValueUpdated += action =>
                {
                    sponsorSkip_interaction.Value = action;
                    if (VideoPlayerSelected?.videoPlayer?.VideoPlayer != null && VideoPlayerSelected.videoPlayer.lastNetworkVideoUrl == sponsorskipVideo)
                        PrepSkipList();
                };
                skipSettings.AddToggle("Intro", "Skip intro segments.<p>Doesn't skip if <5 seconds.", sponsorSkip_intro.Value).OnValueUpdated += action =>
                {
                    sponsorSkip_intro.Value = action;
                    if (VideoPlayerSelected?.videoPlayer?.VideoPlayer != null && VideoPlayerSelected.videoPlayer.lastNetworkVideoUrl == sponsorskipVideo)
                        PrepSkipList();
                };

                if (!sponsorSkip)
                {
                    SponsorSkipEvents.AddCategory($"SponsorBlock not enabled");
                }
                else if (VideoPlayerSelected?.videoPlayer?.VideoPlayer == null)
                {
                    SponsorSkipEvents.AddCategory($"No video player selected");
                }
                else if (sponsorskipVideo == "API_Repsonse_Not_Found")
                {
                    SponsorSkipEvents.AddCategory($"Video not found in SponsorBlock API");
                }
                else if (VideoPlayerSelected.videoPlayer.lastNetworkVideoUrl != sponsorskipVideo)
                {
                    SponsorSkipEvents.AddCategory($"SponsorBlock hasn't grabbed current video yet, or no video is playing");
                    SponsorSkipEvents.AddCategory($"Go back a page to refresh this menu");
                }
                else if (sponsorskipResult == null || sponsorskipResult.Length == 0)
                {
                    SponsorSkipEvents.AddCategory($"SponsorBlock didn't find any results");
                }
                else
                {
                    var skipHeader = SponsorSkipEvents.AddCategory($"------------ All events for video ------------");

                    foreach (var x in sponsorskipResult.OrderBy(x => x.segment[0]))
                    {
                        SponsorSkipEvents.AddCategory($"Type: {Utils.SkipCatSwitch(x.category)}{((x.category == "poi_highlight") ? "   <<<<<<<<<<<<<<" : "")}");
                        if (x.description != "") SponsorSkipEvents.AddCategory($"Desc: {x.description}");
                        var skipEvent = SponsorSkipEvents.AddCategory($"{Utils.FormatTime(x.segment[0])} - {Utils.FormatTime(x.segment[1])}");

                        skipEvent.AddButton($"Jump to Start", "VideoPlayerModArrow-Left", $"Set video to start time of event {Utils.FormatTime(x.segment[0])}").OnPress += () =>
                        {
                            if (VideoPlayerSelected?.videoPlayer?.VideoPlayer != null && VideoPlayerSelected.videoPlayer.lastNetworkVideoUrl == sponsorskipVideo)
                            {//Confirm this video is still on
                                MelonLogger.Msg($"|SponsorBlock| Manual skip to: {Utils.FormatTime(x.segment[0])}, Was at: {Utils.FormatTime((float)VideoPlayerSelected.videoPlayer.VideoPlayer.Time)}");
                                QuickMenuAPI.ShowAlertToast($"Manual skip to: { Utils.FormatTime(x.segment[0])}", 2);
                                lastskip = x.segment;
                                VideoPlayerSelected.videoPlayer.SetVideoTimestamp(x.segment[0]);
                            }
                            else
                            {
                                QuickMenuAPI.ShowAlertToast("Video Player does not exist or wrong video playing.", 3);
                                MelonLogger.Msg("Video Player Not Selected or does not exist or wrong video playing - skipEvent");
                            }
                        };

                        skipEvent.AddButton($"Jump to End", "VideoPlayerModArrow-Right", $"Set video to end time of event {Utils.FormatTime(x.segment[1])}").OnPress += () =>
                        {
                            if (VideoPlayerSelected?.videoPlayer?.VideoPlayer != null && VideoPlayerSelected.videoPlayer.lastNetworkVideoUrl == sponsorskipVideo)
                            {//Confirm this video is still on
                                MelonLogger.Msg($"|SponsorBlock| Manual skip to: {Utils.FormatTime(x.segment[1])}, Was at: {Utils.FormatTime((float)VideoPlayerSelected.videoPlayer.VideoPlayer.Time)}");
                                QuickMenuAPI.ShowAlertToast($"Manual skip to: { Utils.FormatTime(x.segment[1])}", 2);
                                lastskip = x.segment;
                                VideoPlayerSelected.videoPlayer.SetVideoTimestamp(x.segment[1]);
                            }
                            else
                            {
                                QuickMenuAPI.ShowAlertToast("Video Player does not exist or wrong video playing.", 3);
                                MelonLogger.Msg("Video Player Not Selected or does not exist or wrong video playing - skipEvent");
                            }
                        };
                    }
                    SponsorSkipEvents.AddCategory($"");
                    SponsorSkipEvents.AddCategory($"");
                    SponsorSkipEvents.AddCategory($"Uses SponsorBlock data from https://sponsor.ajay.app/");
                }
            }
            //
            AdvancedOptions.AddButton("Save URL", "VideoPlayerModSave", $"Stores the current video URL into the ChilloutVR/{FolderRoot} Folder.").OnPress += () =>
            {
                if (VideoPlayerSelected != null)
                {
                    SaveUrl(VideoPlayerSelected);
                    QuickMenuAPI.ShowAlertToast($"Saved URL! Located in ChilloutVR/{FolderRoot}{FolderConfig}", 3);
                    MelonLogger.Msg($"Saved URL! Located in ChilloutVR/{FolderRoot}{FolderConfig}");
                    PopulateAdvancedButtons();
                }
                else
                {
                    QuickMenuAPI.ShowAlertToast("Video Player Not Selected or does not exist.", 2);
                    MelonLogger.Msg("Video Player Not Selected or does not exist.");
                }
            };
            savedURLsPage = AdvancedOptions.AddPage("Load Saved URLs", "VideoPlayerModLoadURLs", "Load previously saved URLS", "VideoRemoteMod");
            savedURLsPageString = savedURLsPage.ElementID;
            {
                if (savedURLs.Count == 0)
                {
                    savedURLsPage.AddCategory($"No saved URLs found");
                }
                else
                {
                    foreach (var x in savedURLs.OrderBy(x => x.Value.Item2).Reverse())
                    {
                        savedURLsPage.AddCategory(x.Value.Item1);
                        var urlCat = savedURLsPage.AddCategory(x.Value.Item2.ToString("yyyy'-'MM'-'dd"));
                        urlCat.AddButton($"Play Video", "VideoPlayerModPastePlay", $"Play the video: {x.Key}<p>{x.Value.Item1}").OnPress += () =>
                        {
                            if (VideoPlayerSelected != null)
                            {
                                QuickMenuAPI.ShowConfirm("Confirm", $"Play Video?<p><p><p>{x.Key}<p><p><p>{x.Value.Item1}", () =>
                                {
                                    VideoPlayerSelected.videoPlayer.SetVideoUrl(x.Key);
                                    MelonCoroutines.Start(Instance.SetCurrentVideoNameDelay());
                                }, () => { }, "Yes", "No");
                            }
                            else
                            {
                                QuickMenuAPI.ShowAlertToast("Video Player Not Selected or does not exist.", 2);
                                MelonLogger.Msg("Video Player Not Selected or does not exist.");
                            }
                        };
                    }
                }
            }
        }


        private static void SetCurrentVideoName()
        {
            videoName.CategoryName = (VideoPlayerSelected != null) ? "Playing: " + Utils.VideoNameFormat(VideoPlayerSelected) : "No video player selected";
        }
        System.Collections.IEnumerator SetCurrentVideoNameDelay()
        {//Lazy way to set the name after letting the video load
            int i = 0;
            while (i <= 10)
            {
                i++;
                yield return new WaitForSecondsRealtime(1);
                SetCurrentVideoName();
            }
        }

        System.Collections.IEnumerator ReloadVideo()
        {
            TryingToReplay = true;
            if (VideoPlayerSelected?.videoPlayer != null) {
                var existingVideoName = Utils.VideoNameFormat(VideoPlayerSelected);
                var existingURL = VideoPlayerSelected.videoPlayer.lastNetworkVideoUrl;
                var existingTimeStamp = VideoPlayerSelected.videoPlayer.VideoPlayer.Time;
                var timeout = Time.time + 30f;

                MelonLogger.Msg($"Replaying video at Timestamp - Name: {existingVideoName}\n" +
                    $"URL: {existingURL} - TimeStamp: {existingTimeStamp}, TimeStamp: {Utils.FormatTime((float)existingTimeStamp)}");

                VideoPlayerSelected.videoPlayer.SetVideoUrl(existingURL);

                while (Time.time < timeout && VideoPlayerSelected?.videoPlayer != null)
                {
                    if (VideoPlayerSelected.videoPlayer.VideoPlayer.Time < existingTimeStamp && Utils.VideoNameFormat(VideoPlayerSelected) == existingVideoName)
                    {
                        yield return new WaitForSecondsRealtime(2f);//Just to give some buffer
                        MelonLogger.Msg($"Setting timestamp {existingTimeStamp}");
                        VideoPlayerSelected.videoPlayer.SetVideoTimestamp(existingTimeStamp);
                        TryingToReplay = false;
                        break;
                    }
                    yield return new WaitForSecondsRealtime(.5f);
                }
            }
            TryingToReplay = false;
        }

        private static void RefreshMainPage()
        {
            //MelonLogger.Msg(lastQMPage);
            if (lastQMPage == MainPageString)
            {
                PopulateAdvancedButtons();
                SetCurrentVideoName();
                if (VideoPlayerSelected != null)
                {
                    volumeSilder.SetSliderValue(VideoPlayerSelected.videoPlayer.playbackVolume);
                }
            }
        }
        //So many methods to make sure this refreshes on change
        public static void OnPageOpen(string targetPage, string lastPage)
        {
            if (targetPage == VideoFolderString)
            {
                PopulateVideoList();
            }
            if (targetPage == SponsorSkipEventsString || targetPage == AdvOptionsString || targetPage == savedURLsPageString)
            {
                PopulateAdvancedButtons();
            }

            lastQMPage = targetPage;
            RefreshMainPage();
        }
        public static void OnPageBack(string targetPage, string lastPage)
        {
            lastQMPage = targetPage;
            RefreshMainPage();
        }
        private static void QMtoggle(bool __0)
        {
            if (__0) RefreshMainPage();
        }

        private static void ToggleLocalScreen()
        {
            if (!localScreen?.Equals(null) ?? false)
            {
                try { UnityEngine.Object.Destroy(localScreen); } catch (System.Exception ex) { MelonLogger.Msg(ConsoleColor.DarkRed, ex.ToString()); }
                localScreen = null;
            }
            else if (VideoPlayerSelected != null)
            {
                GameObject cam = Camera.main.gameObject;
                Vector3 pos = cam.transform.position + (cam.transform.forward * 2f); // Gets position of Head 
                GameObject _obj = GameObject.CreatePrimitive(PrimitiveType.Plane);
                _obj.transform.position = pos;
                _obj.transform.LookAt(cam.transform);
                _obj.transform.rotation = _obj.transform.rotation * Quaternion.AngleAxis(90, Vector3.right); //Flip so screen is visible 
                _obj.name = "LocalVideoScreen-Mod";
                _obj.transform.localScale = new Vector3(1.777f * sizeScale / 10f, 1f * sizeScale / 10f, 1f * sizeScale / 10f);

                _obj.GetComponent<MeshCollider>().enabled = false;
                _obj.AddComponent<BoxCollider>();
                _obj.GetComponent<BoxCollider>().size = new Vector3(10f, .05f, 10f);
                _obj.GetComponent<BoxCollider>().isTrigger = true;

                Material mat = new Material(Shader.Find("Unlit/Texture"));
                mat.mainTexture = VideoPlayerSelected.videoPlayer.ProjectionTexture;
                _obj.GetComponent<MeshRenderer>().material = mat;

                _obj.AddComponent<CVRPickupObject>();
                _obj.GetComponent<CVRPickupObject>().maximumGrabDistance = 30f;
                _obj.GetComponent<CVRPickupObject>().enabled = pickupable;
                _obj.GetComponent<CVRPickupObject>().gripType = CVRPickupObject.GripType.Free;

                localScreen = _obj;
            }
        }
        private static void UpdateLocalScreen()
        {
            if (!localScreen?.Equals(null) ?? false)
            {
                localScreen.transform.localScale = new Vector3(1.777f * sizeScale / 10f, 1f * sizeScale / 10f, 1f * sizeScale / 10f);
                localScreen.GetComponent<CVRPickupObject>().enabled = pickupable;
            }
        }

        private static void StartSponsorSkip()
        {
            if (sponsorSkipCheckCoroutine != null) MelonCoroutines.Stop(sponsorSkipCheckCoroutine);
            if (sponsorSkip) sponsorSkipCheckCoroutine = MelonCoroutines.Start(SponsorSkipCheck());
        }

        public static System.Collections.IEnumerator SponsorSkipCheck()
        {
            bool startedSkips = false;
            while (sponsorSkip && VideoPlayerSelected?.videoPlayer?.VideoPlayer != null)
            {
                yield return new WaitForSeconds(1f);
                if (VideoPlayerSelected.videoPlayer.lastNetworkVideoUrl != lastvideo && !String.IsNullOrWhiteSpace(VideoPlayerSelected.videoPlayer.lastNetworkVideoUrl))
                {
                    startedSkips = false;
                    string newVid = VideoPlayerSelected.videoPlayer.lastNetworkVideoUrl;
                    lastvideo = newVid;
                    MelonLogger.Msg($"|SponsorBlock| New video found: {newVid}");
                    GetAsync(newVid);               
                }
                else if (VideoPlayerSelected.videoPlayer.lastNetworkVideoUrl == sponsorskipVideo && !startedSkips)
                {
                    MelonLogger.Msg($"|SponsorBlock| Current video has skip info - {sponsorskipVideo}");
                    //MelonLogger.Msg($"|SponsorBlock| Raw");
                    //foreach (var x in sponsorskipResult)
                    //{
                    //    MelonLogger.Msg($"|SponsorBlock| {x.category} Segment: {Utils.FormatTime(x.segment[0])} - {Utils.FormatTime(x.segment[1])} Seconds:{x.segment[0]}-{x.segment[1]}");
                    //}

                    PrepSkipList();
                    startedSkips = true;
                    if (sponsorSkipLiveCoroutine != null) MelonCoroutines.Stop(sponsorSkipLiveCoroutine);
                    sponsorSkipLiveCoroutine = MelonCoroutines.Start(SponsorSkipping());

                    PopulateAdvancedButtons();
                }
            }
            sponsorSkip = false;
        }

        //https://www.youtube.com/watch?v=FxNiVwMQw1E
        //https://sponsor.ajay.app/api/skipSegments?videoID=FxNiVwMQw1E&category=sponsor //Example
        //https://wiki.sponsor.ajay.app/w/API_Docs
        public static async Task GetAsync(string url)
        {
            if (!url.Contains("youtu"))
                return;
            //https://stackoverflow.com/a/39777772
            var uri = new Uri(url);
            var query = HttpUtility.ParseQueryString(uri.Query);
            var videoId = string.Empty;
            if (query.AllKeys.Contains("v"))
            {
                videoId = query["v"];
            }
            else
            {
                videoId = uri.Segments.Last();
            }

            string requestURL = $"https://sponsor.ajay.app/api/skipSegments?videoID={videoId}&category=sponsor&category=selfpromo&category=interaction&category=intro&category=preview&category=music_offtopic&category=poi_highlight&category=chapter";
            //MelonLogger.Msg($"|SponsorBlock| Request URL {requestURL}");
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestURL);
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    var data = await reader.ReadToEndAsync();
                    //MelonLogger.Msg($"|SponsorBlock| Raw data {data}");
                    sponsorskipVideo = url;
                    sponsorskipResult = JsonConvert.DeserializeObject<SponsorSkipSegment[]>(data);
                }  
            }
            catch (Exception ex) {
                MelonLogger.Msg("|SponsorBlock| Couldn't get video from API: " + ex.Message); 
                sponsorskipVideo = "API_Repsonse_Not_Found";

                PopulateAdvancedButtons();
            }
        }

        public static void PrepSkipList()
        {
            sponsorskips.Clear();
            foreach (var x in sponsorskipResult)
            { 
                if((x.category == "sponsor" && sponsorSkip_sponsor.Value)
                   || (x.category == "selfpromo" && sponsorSkip_selfpromo.Value)
                   || (x.category == "interaction" && sponsorSkip_interaction.Value)
                   || (x.category == "intro" && sponsorSkip_intro.Value))
                {
                    sponsorskips.Add((x.category, x.segment));
                }
            }
            //MelonLogger.Msg($"|SponsorBlock| Skips Only");
            //foreach (var x in sponsorskips)
            //{
            //    MelonLogger.Msg($"|SponsorBlock| {x.Item1} Segment: {Utils.FormatTime(x.Item2[0])} - {Utils.FormatTime(x.Item2[1])} Seconds:{x.Item2[0]}-{x.Item2[1]}");
            //}
        }

        public static System.Collections.IEnumerator SponsorSkipping()
        {
            var vidDur = VideoPlayerSelected.videoPlayer.VideoPlayer.Info.VideoMetaData.GetDuration();
            while (sponsorSkip && VideoPlayerSelected?.videoPlayer?.VideoPlayer != null && VideoPlayerSelected.videoPlayer.lastNetworkVideoUrl == sponsorskipVideo)
            {
                yield return new WaitForSeconds(.25f);
                foreach (var x in sponsorskips)
                { //Check every x seconds if within a skip, if so, jump to end of skip
                    if (VideoPlayerSelected.videoPlayer.VideoPlayer.Time > x.Item2[0] && VideoPlayerSelected.videoPlayer.VideoPlayer.Time < x.Item2[1] - 5f  //Don't skip if close to end
                        && x.Item2[0] != lastskip[0] && x.Item2[1] != lastskip[1]//Don't repeat skips
                        && x.Item2[0] < vidDur - 5 && x.Item2[1] < vidDur - 5)//Do not skip to very send
                    {
                        MelonLogger.Msg($"|SponsorBlock| Skipping to: {Utils.FormatTime(x.Item2[1])}, Was at: {Utils.FormatTime((float)VideoPlayerSelected.videoPlayer.VideoPlayer.Time)}, Due to: {Utils.SkipCatSwitch(x.Item1)}");
                        CohtmlHud.Instance.ViewDropText($"|SponsorBlock|", $"{Utils.SkipCatSwitch(x.Item1)} - Skipping to: { Utils.FormatTime(x.Item2[1])}");
                        lastskip = x.Item2;
                        VideoPlayerSelected.videoPlayer.SetVideoTimestamp(x.Item2[1]);
                        break;
                    }
                }
            }
        }
    }

    public class SponsorSkipSegment
    {
        public float[] segment;
        public string UUID;
        public string category;
        public float videoDuration;
        public string actionType;
        public int locked;
        public int votes;
        public string description;
    }
}