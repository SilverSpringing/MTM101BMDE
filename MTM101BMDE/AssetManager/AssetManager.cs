﻿using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.IO;
using BepInEx;
using System.Linq;

namespace MTM101BaldAPI.AssetManager
{
    public static class AssetManager
    {
		public static Texture2D TextureFromFile(string path)
		{
			byte[] array = File.ReadAllBytes(path);
			Texture2D texture2D = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			ImageConversion.LoadImage(texture2D, array);
			texture2D.filterMode = FilterMode.Point;
			return texture2D;
		}

		public static AudioClip AudioClipFromFile(string path)
        {
			return WavDataUtility.ToAudioClip(File.ReadAllBytes(path),Path.GetFileNameWithoutExtension(path));
        }

		public static Texture2D TextureFromMod(BaseUnityPlugin plug, params string[] paths)
        {
			List<string> pathz = paths.ToList();
			pathz.Insert(0, GetModPath(plug));
			return TextureFromFile(Path.Combine(pathz.ToArray()));
        }
		public static AudioClip AudioClipFromMod(BaseUnityPlugin plug, params string[] paths)
		{
			List<string> pathz = paths.ToList();
			pathz.Insert(0, GetModPath(plug));
			return AudioClipFromFile(Path.Combine(pathz.ToArray()));
		}

		public static string GetModPath(BaseUnityPlugin plug)
        {
			return Path.Combine(Application.streamingAssetsPath, "Modded", plug.Info.Metadata.GUID);
		}

	}
}
