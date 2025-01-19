using OpenBrush.Multiplayer;
using TMPro;
using UnityEngine;

public class PlayerListItemPrefab : MonoBehaviour
{
    public TextMeshPro PlayerId;
    public TextMeshPro NickName;
    public RemotePlayer remotePlayer;

    public void SetRemotePlayer(RemotePlayer Player)
    {
        remotePlayer = Player;
        SetPlayerId();
        SetNickname();
    }

    public void SetNickname() { NickName.text = remotePlayer.Nickname; }
    public void SetPlayerId() { PlayerId.text = remotePlayer.PlayerId.ToString(); }

}
