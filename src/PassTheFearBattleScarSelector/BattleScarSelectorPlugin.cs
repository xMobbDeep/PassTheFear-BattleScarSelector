using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using GameFramework.Resource;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Wogame;
using Wogame.Combat;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace PassTheFearBattleScarSelector;

[BepInPlugin("com.passthefear.battle-scar-selector", "Pass The Fear Battle Scar Selector", "1.0.0")]
public sealed class BattleScarSelectorPlugin : BasePlugin
{
    private const string ConfigFileName = "BattleScarSelector.cfg";
    private const string EffectsFileName = "battle_scar_effects.tsv";
    private const string IconFolderName = "icons";
    private static readonly Choice[] DefaultChoices =
    {
        new Choice(51919, 52913),
        new Choice(51202, 52701),
        new Choice(51201, 52104)
    };

    private static readonly HashSet<IntPtr> RoleInfoBeardItemPointers = new HashSet<IntPtr>();
    private static ManualLogSource? logger;
    private static int roleInfoInitialising;
    private static bool effectsExported;
    private static readonly HashSet<int> exportedIconIds = new HashSet<int>();
    private static readonly HashSet<string> loggedIconPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static int iconObserverErrors;
    private static int iconCaptureDiagnostics;
    private static int resourceIconDiagnostics;
    private static readonly HashSet<int> requestedResourceIconIds = new HashSet<int>();
    private static LoadAssetCallbacks? iconLoadCallbacks;
    private static LoadAssetSuccessCallback? iconLoadSuccessCallback;
    private static LoadAssetFailureCallback? iconLoadFailureCallback;
    private static string ConfigPath => Path.Combine(Path.GetDirectoryName(typeof(BattleScarSelectorPlugin).Assembly.Location) ?? Paths.PluginPath, ConfigFileName);

    public override void Load()
    {
        logger = Log;
        EnsureConfigExists();
        Harmony.CreateAndPatchAll(typeof(BattleScarSelectorPlugin), "com.passthefear.battle-scar-selector");
        AddComponent<IconExportObserver>();
        Log.LogInfo($"Battle Scar Selector yüklendi. Ayar dosyası: {ConfigPath}");
    }

    [HarmonyPatch(typeof(AberrationBeardItem), "InitUI")]
    [HarmonyPrefix]
    private static void ShowSelectedCards(AberrationBeardItem __instance, int index, ref OrbsData data, ref bool forceActive)
    {
        if (data == null || index < 0 || index > 2)
            return;

        var choice = ReadChoices()[index];
        var roleInfo = roleInfoInitialising > 0 || (__instance != null && RoleInfoBeardItemPointers.Contains(__instance.Pointer));
        var replacement = new OrbsData(choice.BlessingId, choice.CurseId, data.MapID);
        replacement.IsActive = roleInfo || data.IsActive;
        data = replacement;
        if (roleInfo)
            forceActive = true;
    }

    [HarmonyPatch(typeof(NetworkHero), "Player_AberrationSetOrbs")]
    [HarmonyPrefix]
    private static void ApplySelectedCards(ref Il2CppSystem.Collections.Generic.List<OrbsData> activeOrbs)
    {
        activeOrbs = CreateSelectedCards(activeOrbs);
    }

    [HarmonyPatch(typeof(PlayerEntity), "RefreshOrbs")]
    [HarmonyPrefix]
    private static void ApplySelectedCardsToPlayer(ref Il2CppSystem.Collections.Generic.List<OrbsData> orbs)
    {
        orbs = CreateSelectedCards(orbs);
    }

    // The game's pearl table contains the authoritative localized descriptions.
    // Export it when the Battle Scar pool is first requested so the standalone
    // selector can explain every choice without hard-coded guesses.
    [HarmonyPatch(typeof(OrbsMode), "GetOrbs")]
    [HarmonyPostfix]
    private static void ExportEffectData(OrbsMode __instance)
    {
        TryExportEffectData(__instance);
    }

    [HarmonyPatch(typeof(RoleInfoPanelItem), "InitUI")]
    [HarmonyPrefix]
    private static void PrepareRoleInfo(RoleInfoPanelItem __instance, BattleSetFormData data)
    {
        roleInfoInitialising++;
        RegisterRoleInfoItems(__instance);
        if (data != null)
            data.Orbs = CreateSelectedCards(data.Orbs);
    }

    [HarmonyPatch(typeof(RoleInfoPanelItem), "InitUI")]
    [HarmonyPostfix]
    private static void FinishRoleInfo(RoleInfoPanelItem __instance)
    {
        RegisterRoleInfoItems(__instance);
        if (roleInfoInitialising > 0)
            roleInfoInitialising--;
    }

    [HarmonyPatch(typeof(AberrationBeardItem), "InitUI")]
    [HarmonyPostfix]
    private static void CaptureDisplayedIcons(AberrationBeardItem __instance)
    {
        CaptureDisplayedIconsSafe(__instance);
    }

    internal static void CaptureDisplayedIconsSafe(AberrationBeardItem? item)
    {
        if (item == null)
            return;
        try
        {
            var data = item.Data;
            if (data == null)
            {
                LogCaptureDiagnostic("kart verisi henüz hazır değil");
                return;
            }

            var blessingExported = ExportSprite(data.BlessingId, item.mImg_Blessing);
            var curseExported = ExportSprite(data.CurseId, item.mImg_Curse);
            if (!blessingExported && !curseExported)
                LogCaptureDiagnostic($"kart {data.BlessingId}/{data.CurseId} için sprite henüz hazır değil");
        }
        catch (Exception exception)
        {
            if (iconObserverErrors++ < 3)
                logger?.LogWarning($"Battle Scar ikonu okunamadı: {exception.Message}");
        }
    }

    private static void LogCaptureDiagnostic(string message)
    {
        if (iconCaptureDiagnostics++ < 5)
            logger?.LogInfo($"Battle Scar ikon taraması: {message}.");
    }

    private static Il2CppSystem.Collections.Generic.List<OrbsData> CreateSelectedCards(Il2CppSystem.Collections.Generic.List<OrbsData>? existing)
    {
        var mapId = 10101;
        if (existing != null)
        {
            for (var i = 0; i < existing.Count; i++)
            {
                var card = existing[i];
                if (card != null && card.MapID != 0)
                {
                    mapId = card.MapID;
                    break;
                }
            }
        }

        var cards = new Il2CppSystem.Collections.Generic.List<OrbsData>();
        foreach (var choice in ReadChoices())
        {
            var card = new OrbsData(choice.BlessingId, choice.CurseId, mapId);
            card.IsActive = true;
            cards.Add(card);
        }
        return cards;
    }

    private static void RegisterRoleInfoItems(RoleInfoPanelItem? panel)
    {
        if (panel == null)
            return;
        try
        {
            if (panel.aberrationBeardItem != null)
                RoleInfoBeardItemPointers.Add(panel.aberrationBeardItem.Pointer);
            var items = panel.aberrationBeardItems;
            if (items == null)
                return;
            for (var i = 0; i < items.Count; i++)
                if (items[i] != null)
                    RoleInfoBeardItemPointers.Add(items[i].Pointer);
        }
        catch (Exception exception)
        {
            logger?.LogWarning($"Character info kartları kaydedilemedi: {exception.Message}");
        }
    }

    private static Choice[] ReadChoices()
    {
        var choices = (Choice[])DefaultChoices.Clone();
        try
        {
            EnsureConfigExists();
            var values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadAllLines(ConfigPath))
            {
                var separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;
                var key = line.Substring(0, separator).Trim();
                var value = line.Substring(separator + 1).Trim();
                if (int.TryParse(value, out var id) && id > 0)
                    values[key] = id;
            }
            for (var i = 0; i < choices.Length; i++)
            {
                var slot = i + 1;
                if (values.TryGetValue($"slot{slot}_blessing", out var blessing))
                    choices[i] = new Choice(blessing, choices[i].CurseId);
                if (values.TryGetValue($"slot{slot}_curse", out var curse))
                    choices[i] = new Choice(choices[i].BlessingId, curse);
            }
        }
        catch (Exception exception)
        {
            logger?.LogWarning($"Ayar dosyası okunamadı; varsayılan üçlü kullanılacak: {exception.Message}");
        }
        return choices;
    }

    private static void EnsureConfigExists()
    {
        if (File.Exists(ConfigPath))
            return;
        File.WriteAllText(ConfigPath,
            "# Pass The Fear Battle Scar Selector\r\n" +
            "# Her slot bir blessing ve bir curse ID'si kullanır.\r\n" +
            "slot1_blessing=51919\r\nslot1_curse=52913\r\n" +
            "slot2_blessing=51202\r\nslot2_curse=52701\r\n" +
            "slot3_blessing=51201\r\nslot3_curse=52104\r\n");
    }

    private static void TryExportEffectData(OrbsMode mode)
    {
        if (effectsExported || mode == null)
            return;

        try
        {
            var dictionary = mode._orbDic;
            if (dictionary == null || dictionary.Keys == null || dictionary.Keys.Count == 0)
                return;

            var pearls = new Dictionary<int, GMPearlInfo>();
            foreach (var key in dictionary.Keys)
            {
                var entries = dictionary[key];
                if (entries == null)
                    continue;
                for (var i = 0; i < entries.Count; i++)
                {
                    var pearl = entries[i];
                    if (pearl != null && pearl.Id > 0)
                        pearls[pearl.Id] = pearl;
                }
            }

            if (pearls.Count == 0)
                return;

            var path = Path.Combine(Path.GetDirectoryName(typeof(BattleScarSelectorPlugin).Assembly.Location) ?? Paths.PluginPath, EffectsFileName);
            using (var writer = new StreamWriter(path, false, new System.Text.UTF8Encoding(false)))
            {
                writer.WriteLine("id\ttype\tname\tdescription\teffect_details\ticon");
                foreach (var pearl in pearls.Values.OrderBy(item => item.Id))
                {
                    LogIconPath(pearl.Icon);
                    TryExportResourceIcon(pearl.Id, pearl.Icon);
                    var name = ResolveText(pearl.Name);
                    var description = ResolveText(pearl.Description);
                    var details = CollectEffectDetails(pearl);
                    if (string.IsNullOrWhiteSpace(description) || description == pearl.Description)
                    {
                        if (!string.IsNullOrWhiteSpace(details))
                            description = details;
                    }
                    writer.WriteLine(string.Join("\t", new[]
                    {
                        pearl.Id.ToString(),
                        pearl.Id >= 52000 ? "Curse" : "Blessing",
                        EscapeField(name),
                        EscapeField(description),
                        EscapeField(details),
                        EscapeField(pearl.Icon)
                    }));
                }
            }

            effectsExported = true;
            logger?.LogInfo($"Battle Scar açıklamaları dışa aktarıldı: {path} ({pearls.Count} kayıt).");
        }
        catch (Exception exception)
        {
            logger?.LogWarning($"Battle Scar açıklamaları dışa aktarılamadı: {exception.Message}");
        }
    }

    private static string CollectEffectDetails(GMPearlInfo pearl)
    {
        var details = new List<string>();
        try
        {
            AddEffectList(details, pearl.GetExtraEffectInfo_Play_IDsList(), "Blessing");
            AddEffectList(details, pearl.GetExtraEffectInfo_Enemy_IDsList(), "Curse");
        }
        catch (Exception exception)
        {
            logger?.LogDebug($"Battle Scar efekt ayrıntısı okunamadı ({pearl.Id}): {exception.Message}");
        }
        return string.Join("; ", details);
    }

    private static void AddEffectList(List<string> details, Il2CppSystem.Collections.Generic.List<GMExtraEffectInfo>? effects, string side)
    {
        if (effects == null)
            return;
        for (var i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            if (effect == null)
                continue;
            var label = ResolveText(effect.EffectName);
            var explanation = ResolveText(effect.EsayDescription);
            var text = string.IsNullOrWhiteSpace(explanation) ? label : explanation;
            if (!string.IsNullOrWhiteSpace(text))
                details.Add($"{side}: {text} [effect {effect.Id}]");
        }
    }

    private static string ResolveText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        try
        {
            var localized = Wogame.GameEntry.Localization.GetString(value);
            return string.IsNullOrWhiteSpace(localized) ? value : localized;
        }
        catch
        {
            return value;
        }
    }

    private static string EscapeField(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("\t", "\\t")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    private static bool ExportSprite(int id, Image? image)
    {
        return image != null && ExportSprite(id, image.sprite);
    }

    private static bool ExportSprite(int id, Sprite? sprite)
    {
        if (id <= 0 || sprite == null || exportedIconIds.Contains(id))
            return false;

        var texture = sprite.texture;
        if (texture == null)
            return false;

        var folder = Path.Combine(Path.GetDirectoryName(typeof(BattleScarSelectorPlugin).Assembly.Location) ?? Paths.PluginPath, IconFolderName);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, id.ToString() + ".png");
        if (File.Exists(path))
        {
            exportedIconIds.Add(id);
            return true;
        }

        var png = CopySpriteToPng(sprite);
        if (png == null || png.Length == 0)
            return false;

        File.WriteAllBytes(path, png);
        exportedIconIds.Add(id);
        logger?.LogInfo($"Battle Scar ikonu dışa aktarıldı: {id} -> {path}");
        return true;
    }

    private static void TryExportResourceIcon(int id, string? iconPath)
    {
        if (id <= 0 || string.IsNullOrWhiteSpace(iconPath) || exportedIconIds.Contains(id))
            return;

        try
        {
            var sprite = Resources.Load<Sprite>(iconPath);
            if (ExportSprite(id, sprite))
                return;

            var texture = Resources.Load<Texture2D>(iconPath);
            if (texture != null)
            {
                var fullSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);
                try
                {
                    ExportSprite(id, fullSprite);
                }
                finally
                {
                    UnityEngine.Object.Destroy(fullSprite);
                }
                return;
            }

            if (resourceIconDiagnostics++ < 5)
                logger?.LogInfo($"Battle Scar kaynak sprite'ı Resources içinde bulunamadı: {iconPath}");

            QueueFrameworkResourceIcon(id, iconPath);
        }
        catch (Exception exception)
        {
            if (resourceIconDiagnostics++ < 5)
                logger?.LogDebug($"Battle Scar kaynak sprite'ı okunamadı ({iconPath}): {exception.Message}");

            QueueFrameworkResourceIcon(id, iconPath);
        }
    }

    private static void QueueFrameworkResourceIcon(int id, string iconPath)
    {
        if (id <= 0 || string.IsNullOrWhiteSpace(iconPath) || requestedResourceIconIds.Contains(id))
            return;

        try
        {
            var resource = Wogame.GameEntry.Resource;
            if (resource == null)
                return;

            requestedResourceIconIds.Add(id);

            iconLoadSuccessCallback ??= DelegateSupport.ConvertDelegate<LoadAssetSuccessCallback>(
                new Action<string, object, float, object>(OnIconLoadSuccess));
            iconLoadFailureCallback ??= DelegateSupport.ConvertDelegate<LoadAssetFailureCallback>(
                new Action<string, LoadResourceStatus, string, object>(OnIconLoadFailure));
            iconLoadCallbacks ??= new LoadAssetCallbacks(iconLoadSuccessCallback, iconLoadFailureCallback);
            resource.LoadAsset(iconPath, iconLoadCallbacks, id);
        }
        catch (Exception exception)
        {
            if (resourceIconDiagnostics++ < 5)
                logger?.LogDebug($"Battle Scar kaynak yükleme kuyruğa alınamadı ({iconPath}): {exception.Message}");
        }
    }

    private static void OnIconLoadSuccess(string assetName, object asset, float duration, object userData)
    {
        try
        {
            var id = userData is int value ? value : ParseIconId(assetName);
            if (asset is Sprite sprite)
            {
                ExportSprite(id, sprite);
                return;
            }

            if (asset is Texture2D texture)
            {
                ExportTexture(id, texture);
                return;
            }

            if (resourceIconDiagnostics++ < 5)
                logger?.LogInfo($"Battle Scar kaynağı sprite değil: {assetName} ({asset?.GetType().Name ?? "null"}).");
        }
        catch (Exception exception)
        {
            if (resourceIconDiagnostics++ < 5)
                logger?.LogDebug($"Battle Scar kaynak sprite'ı dışa aktarılamadı ({assetName}): {exception.Message}");
        }
    }

    private static void OnIconLoadFailure(string assetName, LoadResourceStatus status, string errorMessage, object userData)
    {
        if (resourceIconDiagnostics++ < 5)
            logger?.LogInfo($"Battle Scar kaynağı yüklenemedi: {assetName} ({status}) {errorMessage}");
    }

    private static int ParseIconId(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return 0;
        var part = assetName.TrimEnd('/').Split('/').LastOrDefault();
        return int.TryParse(part, out var id) ? id : 0;
    }

    private static void ExportTexture(int id, Texture2D texture)
    {
        if (id <= 0 || texture == null || exportedIconIds.Contains(id))
            return;

        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
        try
        {
            ExportSprite(id, sprite);
        }
        finally
        {
            UnityEngine.Object.Destroy(sprite);
        }
    }

    private static byte[]? CopySpriteToPng(Sprite sprite)
    {
        var texture = sprite.texture;
        var rect = sprite.textureRect;
        var x = Math.Max(0, (int)Math.Floor(rect.x));
        var y = Math.Max(0, (int)Math.Floor(rect.y));
        var width = Math.Min(texture.width - x, Math.Max(1, (int)Math.Ceiling(rect.width)));
        var height = Math.Min(texture.height - y, Math.Max(1, (int)Math.Ceiling(rect.height)));
        if (width <= 0 || height <= 0)
            return null;

        try
        {
            if (texture.isReadable)
            {
                var source = texture.GetPixels32();
                var cropped = new Color32[width * height];
                for (var row = 0; row < height; row++)
                    Array.Copy(source, (y + row) * texture.width + x, cropped, row * width, width);
                var copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
                copy.SetPixels32(cropped);
                copy.Apply(false, true);
                var png = ImageConversion.EncodeToPNG(copy);
                UnityEngine.Object.Destroy(copy);
                return png;
            }
        }
        catch (Exception exception)
        {
            logger?.LogDebug($"Okunabilir sprite kopyası başarısız ({sprite.name}): {exception.Message}");
        }

        // Packed atlases are often not CPU-readable. Render the atlas once and crop the sprite.
        RenderTexture? previous = null;
        RenderTexture? render = null;
        Texture2D? readable = null;
        try
        {
            render = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(texture, render);
            previous = RenderTexture.active;
            RenderTexture.active = render;
            readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(x, y, width, height), 0, 0, false);
            readable.Apply(false, true);
            return ImageConversion.EncodeToPNG(readable);
        }
        catch (Exception exception)
        {
            logger?.LogDebug($"RenderTexture sprite kopyası başarısız ({sprite.name}): {exception.Message}");
            return null;
        }
        finally
        {
            if (readable != null)
                UnityEngine.Object.Destroy(readable);
            RenderTexture.active = previous;
            if (render != null)
                RenderTexture.ReleaseTemporary(render);
        }
    }

    internal static void LogIconPath(string iconPath)
    {
        if (!string.IsNullOrWhiteSpace(iconPath) && loggedIconPaths.Add(iconPath))
            logger?.LogInfo($"Battle Scar Icon yolu: {iconPath}");
    }

    internal static void LogObserverError(Exception exception)
    {
        if (iconObserverErrors++ < 3)
            logger?.LogWarning($"Battle Scar ikon taraması hata verdi: {exception.Message}");
    }

    internal static void LogObserverStatus(int formCount, int itemCount, int scanCount)
    {
        logger?.LogInfo($"Battle Scar ikon taraması etkin: {formCount} form, {itemCount} kart, {scanCount} tarama.");
    }

    private readonly struct Choice
    {
        public Choice(int blessingId, int curseId)
        {
            BlessingId = blessingId;
            CurseId = curseId;
        }

        public int BlessingId { get; }
        public int CurseId { get; }
    }
}

public sealed class IconExportObserver : MonoBehaviour
{
    private int frame;
    private int scanCount;
    private bool statusLogged;

    public IconExportObserver(IntPtr pointer) : base(pointer) { }

    public void Update()
    {
        // Asset loading is asynchronous; check occasionally rather than every frame.
        if (++frame % 15 != 0)
            return;
        try
        {
            scanCount++;
            var directForms = Resources.FindObjectsOfTypeAll<AberrationForm>();
            var itemCount = 0;
            for (var i = 0; i < directForms.Length; i++)
            {
                var items = directForms[i].allBeardItems;
                if (items == null)
                    continue;
                itemCount += items.Count;
                for (var j = 0; j < items.Count; j++)
                    BattleScarSelectorPlugin.CaptureDisplayedIconsSafe(items[j]);
            }

            // Some builds keep the form logic alive without exposing it through the
            // UIForm wrapper. Keep the original route as a fallback for those builds.
            if (itemCount == 0)
            {
                var forms = Resources.FindObjectsOfTypeAll<UIForm>();
                for (var i = 0; i < forms.Length; i++)
                {
                    var logic = forms[i].Logic;
                    if (!(logic is AberrationForm aberration))
                        continue;
                    var items = aberration.allBeardItems;
                    if (items == null)
                        continue;
                    itemCount += items.Count;
                    for (var j = 0; j < items.Count; j++)
                        BattleScarSelectorPlugin.CaptureDisplayedIconsSafe(items[j]);
                }
            }

            if (!statusLogged && (itemCount > 0 || scanCount >= 8))
            {
                statusLogged = true;
                BattleScarSelectorPlugin.LogObserverStatus(directForms.Length, itemCount, scanCount);
            }
        }
        catch (Exception exception)
        {
            BattleScarSelectorPlugin.LogObserverError(exception);
        }
    }
}
