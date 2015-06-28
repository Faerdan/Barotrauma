﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Subsurface.Items.Components
{
    class Engine : Powered
    {

        float force;

        float targetForce;

        float maxForce;

        float powerPerForce;

        [Editable, HasDefaultValue(1.0f, true)]
        public float PowerPerForce
        {
            get { return powerPerForce; }
            set
            {
                powerPerForce = Math.Max(0.0f, value);
            }
        }

        [Editable, HasDefaultValue(50000.0f, true)]
        public float MaxForce
        {
            get { return maxForce; }
            set
            {
                maxForce = Math.Max(0.0f, value);
            }
        }

        public float Force
        {
            get { return force;}
            set { force = MathHelper.Clamp(value, -100.0f, 100.0f); }
        }

        public Engine(Item item, XElement element)
            : base(item, element)
        {
            isActive = true;

        }

        public override void Update(float deltaTime, Camera cam)
        {
            base.Update(deltaTime, cam);

            currPowerConsumption = Math.Abs(targetForce) * powerPerForce;

            Force = MathHelper.Lerp(force, (voltage < minVoltage) ? 0.0f : targetForce, 0.1f);
            if (Force != 0.0f)
            {
                Submarine.Loaded.ApplyForce(new Vector2((force / 100.0f) * maxForce * (voltage / minVoltage), 0.0f));
            }
            
            voltage = 0.0f;
        }

        public override void DrawHUD(SpriteBatch spriteBatch, Character character)
        {
            //isActive = true;

            int width = 300, height = 300;
            int x = Game1.GraphicsWidth / 2 - width / 2;
            int y = Game1.GraphicsHeight / 2 - height / 2 - 50;

            GUI.DrawRectangle(spriteBatch, new Rectangle(x, y, width, height), Color.Black, true);

            spriteBatch.DrawString(GUI.font, "Force: " + (int)targetForce+" %", new Vector2(x + 30, y + 30), Color.White);

            if (GUI.DrawButton(spriteBatch, new Rectangle(x + 280, y + 30, 40, 40), "+", true)) targetForce += 1.0f;
            if (GUI.DrawButton(spriteBatch, new Rectangle(x + 280, y + 80, 40, 40), "-", true)) targetForce -= 1.0f;
            
            item.NewComponentEvent(this, true);
        }

        public override void UpdateBroken(float deltaTime, Camera cam)
        {
            force = MathHelper.Lerp(force, 0.0f, 0.1f);
        }

        public override void ReceiveSignal(string signal, Connection connection, Item sender)
        {
            base.ReceiveSignal(signal, connection, sender);

            if (connection.name == "set_force")
            {
                float tempForce;
                if (float.TryParse(signal, NumberStyles.Float, CultureInfo.InvariantCulture, out tempForce))
                {
                    Force = tempForce;
                }
            }  
        }
    }
}
