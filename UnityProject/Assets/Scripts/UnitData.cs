using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace NameSpace
{
    [Serializable]
    public class Anim
    {
        public string name;
        public float inv;
        public Sprite[] sprites;
    }
    public class UnitData : ScriptableObject
    {
        public List<Anim> anims = new List<Anim>();
    }
}