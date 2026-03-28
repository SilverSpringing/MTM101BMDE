using BepInEx;
using HarmonyLib;
using MTM101BaldAPI.AssetTools;
using MTM101BaldAPI.ErrorHandler;
using MTM101BaldAPI.Reflection;
using MTM101BaldAPI.Registers;
using MTM101BaldAPI.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MTM101BaldAPI
{
    public class ModLoadingScreenManager : MonoBehaviour
    {
        internal Sprite barActive = MTM101BaldiDevAPI.AssetMan.Get<Sprite>("Bar");
        internal Sprite barInactive = MTM101BaldiDevAPI.AssetMan.Get<Sprite>("BarTransparent");
        LoadingBar modLoadingBar;
        LoadingBar apiLoadingBar;
        TextMeshProUGUI apiLoadText;
        TextMeshProUGUI modLoadText;
        TextMeshProUGUI modIdText;
        TextMeshProUGUI loadingText;
        TextMeshProUGUI instructionsText;
        List<Image> questionMarks = new List<Image>();
        AudioSource errorAudio;
        bool restarting = false;
        LoadingErrorListener errorListener;

        internal string errorSound = "Activity_Incorrect";
        internal string[] nonstandardErrorSounds = new string[] { "Coughing", "BAL_Ohh", "GlassBreak", "ErrorMaybe", "WeirdError" };
        internal string crashLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CRASH.txt");
        internal string state = "initializing the API";

        public static bool doneLoading = false;
        public static bool errored = false;

        void SetBarValueRaw(LoadingBar bar, int amount)
        {
            for (int i = 0; i < bar.bars.Length; i++)
            {
                bar.bars[i].sprite = (i < amount) ? barActive : barInactive;
            }
        }

        void SetBarValue(LoadingBar bar, float percent)
        {
            SetBarValueRaw(bar, Mathf.FloorToInt(bar.count * percent));
        }

        struct LoadingBar
        {
            public Image[] bars;
            public int count;
        }

        void CreateRandomQMark()
        {
            Sprite selectedSprite = MTM101BaldiDevAPI.Instance.questionMarkSprites[UnityEngine.Random.Range(0, MTM101BaldiDevAPI.Instance.questionMarkSprites.Length)];
            Image newQMark = UIHelpers.CreateImage(selectedSprite, this.transform, new Vector2(UnityEngine.Random.Range(-230f, 230f), UnityEngine.Random.Range(-170f, 170f)), false);
            newQMark.transform.SetAsFirstSibling();
            questionMarks.Add(newQMark);
        }

        void Start()
        {
            errorAudio = this.gameObject.AddComponent<AudioSource>();
            errorAudio.volume = 0.6f;

            errorListener = new LoadingErrorListener();
            errorListener.manager = this;
            BepInEx.Logging.Logger.Listeners.Add(errorListener);

            for (int i = 0; i < 8; i++)
            {
                CreateRandomQMark();
            }

            apiLoadingBar = CreateBar(new Vector2(24f, 164f), 55);
            modLoadingBar = CreateBar(new Vector2(24f, 164f + 80f), 55);

            loadingText = UIHelpers.CreateText<TextMeshProUGUI>(BaldiFonts.ComicSans36, "Loading...", this.transform, new Vector3(24f + (54f * 4f), 98f), true);
            loadingText.color = Color.black;
            loadingText.alignment = TextAlignmentOptions.Center;

            apiLoadText = UIHelpers.CreateText<TextMeshProUGUI>(BaldiFonts.ComicSans24, "", this.transform, new Vector3(24f + (54f * 4f), 164f + 28f), true);
            apiLoadText.color = Color.black;
            apiLoadText.fontStyle = FontStyles.Bold;
            apiLoadText.rectTransform.sizeDelta = new Vector2(480f, 32f);
            apiLoadText.alignment = TextAlignmentOptions.Center;

            modLoadText = UIHelpers.CreateText<TextMeshProUGUI>(BaldiFonts.ComicSans24, "", this.transform, new Vector3(24f + (54f * 4f), 164f + 28f + 80f), true);
            modLoadText.color = Color.black;
            modLoadText.fontStyle = FontStyles.Bold;
            modLoadText.rectTransform.sizeDelta = new Vector2(480f, 32f);
            modLoadText.alignment = TextAlignmentOptions.Center;

            modIdText = UIHelpers.CreateText<TextMeshProUGUI>(BaldiFonts.ComicSans18, "", this.transform, new Vector3(24f + (54f * 4f), 164f + 28f + 80f + 28f), true);
            modIdText.color = Color.black;
            modIdText.rectTransform.sizeDelta = new Vector2(480f, 32f);
            modIdText.alignment = TextAlignmentOptions.Center;

            BeginLoadProcess();
        }

        public void ThrowError(Exception e)
        {
            foreach (Image mark in questionMarks) GameObject.Destroy(mark.gameObject);
            foreach (Image bar in apiLoadingBar.bars) GameObject.Destroy(bar);
            foreach (Image bar in modLoadingBar.bars) GameObject.Destroy(bar);
            GameObject.Destroy(apiLoadText);
            GameObject.Destroy(modLoadText);
            GameObject.Destroy(modIdText);

            errored = true;

            loadingText.color = Color.red;
            loadingText.text = "LOADING STOPPED";
            loadingText.rectTransform.sizeDelta = new Vector2(400, 50);

            instructionsText = UIHelpers.CreateText<TextMeshProUGUI>(BaldiFonts.ComicSans12, "", this.transform, new Vector3(24f + (54f * 4f), 164f + 28f + 80f + 28f), true);
            instructionsText.transform.localPosition = Vector3.zero;
            instructionsText.color = Color.black;
            instructionsText.rectTransform.sizeDelta = new Vector2(360, 96f);
            instructionsText.alignment = TextAlignmentOptions.Center;
            instructionsText.verticalAlignment = VerticalAlignmentOptions.Top;

            instructionsText.text += $"A critical error has occurred while {state}.";
            instructionsText.text += $"\n\nA crash log has been generated at {crashLogPath}.";
            instructionsText.text += $"\n\nMake sure to uninstall the affected mod and update to the latest version of the API.";
            instructionsText.text += "\n\nPLEASE RESTART THE GAME";

            // 10% chance to play a nonstandard error sound

            System.Random rnd = new System.Random();
            int chance = rnd.Next(0, 10);
            if (chance == 0)
            {
                int soundIndex = rnd.Next(0, nonstandardErrorSounds.Count());
                errorAudio.clip = AssetFinder.FindOfTypeWithName<AudioClip>(nonstandardErrorSounds[soundIndex], true);
                errorAudio.clip = AssetFinder.FindOfTypeWithName<AudioClip>(errorSound, true);
            }
            else
            {
                errorAudio.clip = AssetFinder.FindOfTypeWithName<AudioClip>(errorSound, true);
            }
            errorAudio.Play();
            File.WriteAllText(crashLogPath, e.Message + "\n\n" + e.StackTrace);
        }

        void BeginLoadProcess()
        {
            LoadingEvents.SortLoadingEvents();
            StartCoroutine(LoadEnumerator());
        }

        void LoadingEnded()
        {
            Singleton<GlobalCam>.Instance.Transition(UiTransition.Dither, 0.01666667f * 2.5f);
            if (GameObject.Find("NameList")) { GameObject.Find("NameList").GetComponent<AudioSource>().enabled = true; }
            CursorController.Instance.DisableClick(false);
            Destroy(this.gameObject);
        }

        IEnumerator LoadEnumerator()
        {
            yield return BeginLoadEnumerator(MainLoad(), apiLoadingBar, apiLoadText);
            apiLoadText.text = "Done!";
            modIdText.text = "";
            modLoadText.text = "";
            doneLoading = true;
            LoadingEnded();
            yield break;
        }

        IEnumerator BeginLoadEnumerator(IEnumerator numerator, LoadingBar barToAdjust, TMP_Text textToChange)
        {
            if (!numerator.MoveNext())
            {
                ThrowError(new Exception("\"IEnumerator provided to loading ended before expected time!\""));
                yield break;
            }
            int maxSteps = ((int)numerator.Current); // loading method calculated max, yeah.
            if (!numerator.MoveNext())
            {
                ThrowError(new Exception("\"IEnumerator provided to loading ended before expected time!\""));
                yield break;
            }
            textToChange.text = (string)numerator.Current;
            int totalSteps = 0;
            SetBarValue(barToAdjust, 0f);
            yield return null; // not having this here caused an issue where if something only took a brief moment the old text would carry over

            while (numerator.MoveNext())
            {
                if (numerator.Current.GetType() != typeof(string))
                {
                    // in the rare case we genuinely can not predict what will happen next (LootTables...)
                    if (numerator.Current.GetType() == typeof(int))
                    {
                        maxSteps += (int)numerator.Current;
                    }
                    else
                    {
                        yield return numerator.Current;
                    }
                }
                else
                {
                    textToChange.text = (string)numerator.Current;
                    totalSteps++;
                    if (!errored) CreateRandomQMark();
                    SetBarValue(barToAdjust, (float)totalSteps / (float)maxSteps);
                }
                yield return null;
            }

            SetBarValue(barToAdjust, 1f); //incase it returns early, still set the bar to full
            yield break;
        }

        static readonly FieldInfo _potentialItems = AccessTools.Field(typeof(FieldTripBaseRoomFunction), "potentialItems");
        static readonly FieldInfo _guaranteedItems = AccessTools.Field(typeof(FieldTripBaseRoomFunction), "guaranteedItems");

        IEnumerator ModifyFieldtripLoot(FieldTripObject trip)
        {
            yield return GeneratorManagement.fieldtripLootChanges.Count;
            yield return "Loading...";
            FieldTripBaseRoomFunction roomFunction = trip.tripHub.room.roomFunctionContainer.GetComponent<FieldTripBaseRoomFunction>();
            FieldTripLoot tripLoot = new FieldTripLoot();
            tripLoot.potentialItems = ((WeightedItemObject[])_potentialItems.GetValue(roomFunction)).ToList();
            tripLoot.guaranteedItems = ((List<ItemObject>)_guaranteedItems.GetValue(roomFunction)).ToList();
            foreach (KeyValuePair<BaseUnityPlugin, Action<FieldTrips, FieldTripLoot>> kvp in GeneratorManagement.fieldtripLootChanges)
            {
                yield return kvp.Key;
                kvp.Value.Invoke(trip.trip, tripLoot);
            }
            if (GeneratorManagement.fieldtripLootChanges.Count > 0)
            {
                trip.MarkAsNeverUnload();
                trip.tripHub.room.MarkAsNeverUnload();
                trip.tripHub.room.roomFunctionContainer.MarkAsNeverUnload();
            }
            _potentialItems.SetValue(roomFunction, tripLoot.potentialItems.ToArray());
            _guaranteedItems.SetValue(roomFunction, tripLoot.guaranteedItems.ToList());
        }

        IEnumerator MainLoad()
        {
            SceneObject[] objs = Resources.FindObjectsOfTypeAll<SceneObject>().Where(x =>
            {
                if (x.levelObject != null)
                {
                    return true;
                }
                if (x.randomizedLevelObject != null)
                {
                    return x.randomizedLevelObject.Length > 0;
                }
                return false;
            }).ToArray();
            List<SceneObject> objList = new List<SceneObject>(objs);
            objList.Sort((a,b) => (b.manager is MainGameManager).CompareTo((a.manager is MainGameManager)));
            objs = objList.ToArray();
            FieldTripObject[] foundTrips = Resources.FindObjectsOfTypeAll<FieldTripObject>().Where(x => x.tripHub != null).ToArray(); // ignore junk
            yield return (5 + objs.Length) + LoadingEvents.LoadingEventsPost.Count + LoadingEvents.LoadingEventsPre.Count + LoadingEvents.LoadingEventsStart.Count + foundTrips.Length + LoadingEvents.LoadingEventsFinal.Count;
            for (int i = 0; i < LoadingEvents.LoadingEventsStart.Count; i++)
            {
                LoadingEvents.LoadingEvent load = LoadingEvents.LoadingEventsStart[i];
                modIdText.text = load.info.Metadata.GUID;
                state = $"loading mod assets for {load.info.Metadata.GUID}";
                yield return "Loading Mod Assets... (" + i + "/" + LoadingEvents.LoadingEventsStart.Count + ")";
                BeginLoadEnumerator(load.loadingNumerator, modLoadingBar, modLoadText);
            }
            modLoadText.text = "";
            modIdText.text = "";
            yield return "Converting LevelObjects to CustomLevelObjects...";
            MTM101BaldiDevAPI.Instance.ConvertAllLevelObjects();
            for (int i = 0; i < LoadingEvents.LoadingEventsPre.Count; i++)
            {
                LoadingEvents.LoadingEvent load = LoadingEvents.LoadingEventsPre[i];
                modIdText.text = load.info.Metadata.GUID;
                state = $"pre-loading mod assets for {load.info.Metadata.GUID}";
                yield return "Invoking Mod Asset Pre-Loading... (" + i + "/" + LoadingEvents.LoadingEventsPre.Count + ")";
                BeginLoadEnumerator(load.loadingNumerator, modLoadingBar, modLoadText);
            }
            modLoadText.text = "";
            modIdText.text = "";
            MTM101BaldiDevAPI.tooLateForGeneratorBasedFeatures = true;
            foreach (SceneObject obj in objs)
            {
                yield return "Changing " + obj.levelTitle + "...";
                state = $"changing SceneObject {obj.levelTitle}";
                if (obj.levelObject != null)
                {
                    if (!(obj.levelObject is CustomLevelObject))
                    {
                        MTM101BaldiDevAPI.Log.LogWarning(String.Format("Can't invoke SceneObject({0})({2}) Generation Changes for {1}! Not a CustomLevelObject!", obj.levelTitle, obj.levelObject.ToString(), obj.name));
                        continue;
                    }
                }
                if (obj.randomizedLevelObject != null)
                {
                    for (int i = 0; i < obj.randomizedLevelObject.Length; i++)
                    {
                        if (!(obj.randomizedLevelObject[i].selection is CustomLevelObject))
                        {
                            MTM101BaldiDevAPI.Log.LogWarning(String.Format("Can't invoke SceneObject({0})({2}) Generation Changes for {1}! Not a CustomLevelObject!", obj.levelTitle, obj.randomizedLevelObject[i].selection.ToString(), obj.name));
                            continue;
                        }
                    }
                }
                MTM101BaldiDevAPI.Log.LogInfo(String.Format("Invoking SceneObject({0})({1}) Generation Changes!", obj.levelTitle, obj.name));
                try
                {
                    GeneratorManagement.Invoke(obj.levelTitle, obj.levelNo, obj);
                }
                catch (Exception exception)
                {
                    ThrowError(exception);
                }
            }
            yield return "Changing modded SceneObjects...";
            GeneratorManagement.queuedModdedScenes.Sort((a, b) => (b.manager is MainGameManager).CompareTo((a.manager is MainGameManager)));
            while (GeneratorManagement.queuedModdedScenes.Count > 0)
            {
                SceneObject obj = GeneratorManagement.queuedModdedScenes[0];
                state = $"changing modded SceneObject {obj.levelTitle}";
                GeneratorManagement.queuedModdedScenes.RemoveAt(0);
                MTM101BaldiDevAPI.Log.LogInfo(String.Format("Invoking SceneObject({0})({1}) Generation Changes!", obj.levelTitle, obj.name));
                try
                {
                    GeneratorManagement.Invoke(obj.levelTitle, obj.levelNo, obj);
                }
                catch (Exception exception)
                {
                    ThrowError(exception);
                }
            }

            foreach (FieldTripObject trip in foundTrips)
            {
                state = $"changing field trip loot for {trip.name}";
                yield return "Changing " + trip.name + " loot...";
                BeginLoadEnumerator(ModifyFieldtripLoot(trip), modLoadingBar, modLoadText);
            }
            modLoadText.text = "";
            modIdText.text = "";
            yield return "Adding MIDIs...";
            foreach (KeyValuePair<string, byte[]> kvp in AssetLoader.MidisToBeAdded)
            {
                state = $"adding MIDI {kvp.Key}";
                AssetLoader.MidiFromBytes(kvp.Key, kvp.Value);
            }
            AssetLoader.MidisToBeAdded = null;
            for (int i = 0; i < LoadingEvents.LoadingEventsPost.Count; i++)
            {
                LoadingEvents.LoadingEvent load = LoadingEvents.LoadingEventsPost[i];
                modIdText.text = load.info.Metadata.GUID;
                state = $"post-loading mod assets for {load.info.Metadata.GUID}";
                yield return "Invoking Mod Asset Post-Loading... (" + i + "/" + LoadingEvents.LoadingEventsPost.Count + ")";
                BeginLoadEnumerator(load.loadingNumerator, modLoadingBar, modLoadText);
            }
            yield return "Reloading Localization...";
            Singleton<LocalizationManager>.Instance.ReflectionInvoke("Start", null);
            yield return "Reloading highscores...";
            if (MTM101BaldiDevAPI.highscoreHandler == SavedGameDataHandler.Unset)
            {
                if (ModdedHighscoreManager.tagList.Count > 0)
                {
                    MTM101BaldiDevAPI.highscoreHandler = SavedGameDataHandler.Modded;
                }
                else
                {
                    MTM101BaldiDevAPI.highscoreHandler = SavedGameDataHandler.Vanilla;
                }
            }
            Singleton<HighScoreManager>.Instance.Load(); //reload
            for (int i = 0; i < LoadingEvents.LoadingEventsFinal.Count; i++)
            {
                LoadingEvents.LoadingEvent load = LoadingEvents.LoadingEventsFinal[i];
                modIdText.text = load.info.Metadata.GUID;
                state = $"invoking mod asset finalizing for {load.info.Metadata.GUID}";
                yield return "Invoking Mod Asset Finalizing... (" + i + "/" + LoadingEvents.LoadingEventsFinal.Count + ")";
                BeginLoadEnumerator(load.loadingNumerator, modLoadingBar, modLoadText);
            }
            yield break;
        }

        LoadingBar CreateBar(Vector2 position, int length)
        {
            List<Image> sprites = new List<Image>();
            for (int i = 0; i < length; i++)
            {
                sprites.Add(UIHelpers.CreateImage(barInactive, this.transform, position + ((Vector2.right * 8) * i), true));
            }
            return new LoadingBar()
            {
                bars = sprites.ToArray(),
                count = sprites.Count
            };
        }
    }
}
