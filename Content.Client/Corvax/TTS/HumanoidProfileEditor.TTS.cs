using System.Linq;
using Content.Client.Lobby;
using Content.Corvax.Interfaces.Shared;
using Content.Shared.Corvax.TTS;
using Content.Shared.Preferences;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private ISharedSponsorsManager? _sponsorsMgr;
    private List<TTSVoicePrototype> _voiceList = new();

    private void InitializeVoice()
    {
        _voiceList = _prototypeManager
            .EnumeratePrototypes<TTSVoicePrototype>()
            .Where(o => o.RoundStart)
            .OrderBy(o => Loc.GetString(o.Name))
            .ToList();

        CVoiceButton.OnItemSelected += args =>
        {
            CVoiceButton.SelectId(args.Id);
            SetVoice(_voiceList[args.Id].ID);
        };

        CVoicePlayButton.OnPressed += _ => { UserInterfaceManager.GetUIController<LobbyUIController>().PlayTTS(); };
        IoCManager.Instance!.TryResolveType(out _sponsorsMgr);
    }

    private void UpdateTTSVoicesControls()
    {
        if (Profile is null)
            return;

        CVoiceButton.Clear();

        var firstVoiceChoiceId = 1;
        for (var i = 0; i < _voiceList.Count; i++)
        {
            var voice = _voiceList[i];
            if (!HumanoidCharacterProfile.CanHaveVoice(voice, Profile.Sex))
                continue;

            var name = Loc.GetString(voice.Name);
            CVoiceButton.AddItem(name, i);

            if (firstVoiceChoiceId == 1)
                firstVoiceChoiceId = i;

            if (_sponsorsMgr is null)
                continue;
            if (voice.SponsorOnly && _sponsorsMgr != null &&
                !_sponsorsMgr.GetClientPrototypes().Contains(voice.ID))
            {
                CVoiceButton.SetItemDisabled(CVoiceButton.GetIdx(i), true);
            }
        }

        var voiceChoiceId = _voiceList.FindIndex(x => x.ID == Profile.Voice);
        if (!CVoiceButton.TrySelectId(voiceChoiceId) &&
            CVoiceButton.TrySelectId(firstVoiceChoiceId))
        {
            SetVoice(_voiceList[firstVoiceChoiceId].ID);
        }
    }
}
