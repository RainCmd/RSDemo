using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace NameSpace
{
    public class Room : MonoBehaviour
    {
        public Text roomName;
        public Hall hall;
        public RemoteInfo remoteInfo;
        public void SetValue(Hall hall,RemoteInfo info)
        {
            this.hall = hall;
            remoteInfo = info;
            roomName.text = info.name;
        }
        public void OnClick()
        {
            hall.OnClickRoom(remoteInfo);
        }
    }
}