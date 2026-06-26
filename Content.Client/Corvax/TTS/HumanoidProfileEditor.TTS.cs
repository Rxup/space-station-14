using Content.Client.Corvax.TTS;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.TTS;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private TTSTab? _ttsTab;

    private void InitializeTts()
    {
        _cfgManager.OnValueChanged(CCCVars.TTSEnabled, OnTtsEnabledChanged);
        TryRefreshVoiceTab();
    }

    private void ShutdownTts()
    {
        _cfgManager.UnsubValueChanged(CCCVars.TTSEnabled, OnTtsEnabledChanged);
    }

    private void OnTtsEnabledChanged(bool _)
    {
        TryRefreshVoiceTab();
        UpdateTTSVoicesControls();
    }

    private void TryRefreshVoiceTab()
    {
        RemoveVoiceTab();

        if (!_cfgManager.GetCVar(CCCVars.TTSEnabled))
            return;

        _ttsTab = new TTSTab();
        var children = new List<Control>();
        foreach (var child in TabContainer.Children)
            children.Add(child);

        TabContainer.RemoveAllChildren();

        for (var i = 0; i < children.Count; i++)
        {
            if (i == 1)
                TabContainer.AddChild(_ttsTab);

            TabContainer.AddChild(children[i]);
        }

        TabContainer.SetTabTitle(1, Loc.GetString("humanoid-profile-editor-voice-tab"));
        TabContainer.SetTabTitle(2, Loc.GetString("humanoid-profile-editor-jobs-tab"));
        TabContainer.SetTabTitle(3, Loc.GetString("humanoid-profile-editor-antags-tab"));
        TabContainer.SetTabTitle(4, Loc.GetString("humanoid-profile-editor-traits-tab"));
        TabContainer.SetTabTitle(5, Loc.GetString("humanoid-profile-editor-markings-tab"));

        _ttsTab.OnVoiceSelected += voiceId =>
        {
            SetVoice(voiceId);
            _ttsTab.SetSelectedVoice(voiceId);
        };

        _ttsTab.OnPreviewRequested += voiceId =>
        {
            _entManager.System<TTSSystem>().RequestGlobalTTS(Shared.Backmen.TTS.VoiceRequestType.Preview, voiceId);
        };
    }

    private void RemoveVoiceTab()
    {
        if (_ttsTab == null)
            return;

        TabContainer.RemoveChild(_ttsTab);
        _ttsTab.Dispose();
        _ttsTab = null;

        TabContainer.SetTabTitle(1, Loc.GetString("humanoid-profile-editor-jobs-tab"));
        TabContainer.SetTabTitle(2, Loc.GetString("humanoid-profile-editor-antags-tab"));
        TabContainer.SetTabTitle(3, Loc.GetString("humanoid-profile-editor-traits-tab"));
        TabContainer.SetTabTitle(4, Loc.GetString("humanoid-profile-editor-markings-tab"));
    }

    private void UpdateTTSVoicesControls()
    {
        if (Profile is null || _ttsTab is null)
            return;

        _ttsTab.UpdateControls(Profile, Profile.Sex);
        _ttsTab.SetSelectedVoice(Profile.Voice);
    }

    private void SetVoice(ProtoId<TTSVoicePrototype> newVoice)
    {
        Profile = Profile?.WithVoice(newVoice);
        IsDirty = true;
    }
}
