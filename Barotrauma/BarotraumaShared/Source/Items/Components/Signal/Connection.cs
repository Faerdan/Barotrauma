﻿using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Barotrauma.Items.Components
{
    partial class Connection
    {
        //how many wires can be linked to a single connector
        public const int MaxLinked = 5;

        public readonly string Name;

        public Wire[] Wires;

        private Item item;

        public readonly bool IsOutput;

        private static Wire draggingConnected;

        private List<StatusEffect> effects;

        public readonly ushort[] wireId;

        public bool IsPower
        {
            get;
            private set;
        }

        public List<Connection> Recipients
        {
            get
            {
                List<Connection> recipients = new List<Connection>();
                for (int i = 0; i < MaxLinked; i++)
                {
                    if (Wires[i] == null) continue;
                    Connection recipient = Wires[i].OtherConnection(this);
                    if (recipient != null) recipients.Add(recipient);
                }
                return recipients;
            }
        }

        public Item Item
        {
            get { return item; }
        }

        public Connection(XElement element, Item item)
        {

#if CLIENT
            if (connector == null)
            {
                panelTexture = Sprite.LoadTexture("Content/Items/connectionpanel.png");

                connector = new Sprite(panelTexture, new Rectangle(470, 102, 19, 43), Vector2.Zero, 0.0f);
                connector.Origin = new Vector2(9.5f, 10.0f);

                wireVertical = new Sprite(panelTexture, new Rectangle(408, 1, 11, 102), Vector2.Zero, 0.0f);
        }
#endif

            this.item = item;

            //recipient = new Connection[MaxLinked];
            Wires = new Wire[MaxLinked];

            IsOutput = (element.Name.ToString() == "output");
            Name = ToolBox.GetAttributeString(element, "name", (IsOutput) ? "output" : "input");

            IsPower = Name == "power_in" || Name == "power" || Name == "power_out";

            effects = new List<StatusEffect>();

            wireId = new ushort[MaxLinked];

            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "link":
                        int index = -1;
                        for (int i = 0; i < MaxLinked; i++)
                        {
                            if (wireId[i] < 1) index = i;
                        }
                        if (index == -1) break;

                        int id = ToolBox.GetAttributeInt(subElement, "w", 0);
                        if (id < 0) id = 0;
                        wireId[index] = (ushort)id;

                        break;

                    case "statuseffect":
                        effects.Add(StatusEffect.Load(subElement));
                        break;
                }
            }
        }

        public int FindEmptyIndex()
        {
            for (int i = 0; i < MaxLinked; i++)
            {
                if (Wires[i] == null) return i;
            }
            return -1;
        }

        //public int FindLinkIndex(Item item)
        //{
        //    for (int i = 0; i < MaxLinked; i++)
        //    {
        //        if (item == null && recipient[i] == null) return i;
        //        if (recipient[i]!=null && recipient[i].item == item) return i;
        //    }
        //    return -1;
        //}

        public int FindWireIndex(Item wireItem)
        {
            for (int i = 0; i < MaxLinked; i++)
            {
                if (Wires[i] == null && wireItem == null) return i;
                if (Wires[i] != null && Wires[i].Item == wireItem) return i;
            }
            return -1;
        }

        public void TryAddLink(Wire wire)
        {
            for (int i = 0; i < MaxLinked; i++)
            {
                if (Wires[i] == null)
                {
                    Wires[i] = wire;
                    return;
                }
            }
        }

        public void AddLink(int index, Wire wire)
        {
            Wires[index] = wire;
        }
        
        public void SendSignal(int stepsTaken, string signal, Item source, Character sender, float power)
        {
            for (int i = 0; i < MaxLinked; i++)
            {
                if (Wires[i] == null) continue;

                Connection recipient = Wires[i].OtherConnection(this);
                if (recipient == null) continue;
                if (recipient.item == this.item || recipient.item == source) continue;

                foreach (ItemComponent ic in recipient.item.components)
                {
                    ic.ReceiveSignal(stepsTaken, signal, recipient, item, sender, power);
                }

                foreach (StatusEffect effect in recipient.effects)
                {

                    //effect.Apply(ActionType.OnUse, 1.0f, recipient.item, recipient.item);
                    recipient.item.ApplyStatusEffect(effect, ActionType.OnUse, 1.0f);
                }
            }
        }

        public void ClearConnections()
        {
            for (int i = 0; i < MaxLinked; i++)
            {
                if (Wires[i] == null) continue;

                Wires[i].RemoveConnection(this);
                Wires[i] = null;
            }
        }
        
        public void ConnectLinked()
        {
            if (wireId == null) return;
            
            for (int i = 0; i < MaxLinked; i++)
            {
                if (wireId[i] == 0) continue;

                Item wireItem = MapEntity.FindEntityByID(wireId[i]) as Item;

                if (wireItem == null) continue;
                Wires[i] = wireItem.GetComponent<Wire>();

                if (Wires[i] != null)
                {
                    if (Wires[i].Item.body != null) Wires[i].Item.body.Enabled = false;
                    Wires[i].Connect(this, false, false);
                }
            }
        }

    }
}