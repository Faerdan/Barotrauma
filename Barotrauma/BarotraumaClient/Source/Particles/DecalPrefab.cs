﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Barotrauma.Particles
{
    class DecalPrefab
    {
        public readonly string Name;

        public readonly List<Sprite> Sprites;

        public readonly Color Color;

        public readonly float LifeTime;
        public readonly float FadeOutTime;
        public readonly float FadeInTime;

        public DecalPrefab(XElement element)
        {
            Name = element.Name.ToString();

            Sprites = new List<Sprite>();

            foreach (XElement subElement in element.Elements())
            {
                if (subElement.Name.ToString().ToLowerInvariant() == "sprite")
                {
                    Sprites.Add(new Sprite(subElement));             
                }
            }  
            
            Color = new Color(ToolBox.GetAttributeVector4(element, "color", Vector4.One));

            LifeTime = ToolBox.GetAttributeFloat(element, "lifetime", 10.0f);
            FadeOutTime = Math.Min(LifeTime, ToolBox.GetAttributeFloat(element, "fadeouttime", 1.0f));
            FadeInTime = Math.Min(LifeTime - FadeOutTime, ToolBox.GetAttributeFloat(element, "fadeintime", 0.0f));
        }
    }
}
