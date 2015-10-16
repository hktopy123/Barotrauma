﻿using System;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Barotrauma.Items.Components
{
    class PowerContainer : Powered
    {
        //[power/min]        
        float capacity;

        float charge;

        //how fast the battery can be recharged
        float maxRechargeSpeed;

        //how fast it's currently being recharged (can be changed, so that
        //charging can be slowed down or disabled if there's a shortage of power)
        float rechargeSpeed;

        float maxOutput;

        [Editable, HasDefaultValue(10.0f, true)]
        public float MaxOutPut
        {
            set { maxOutput = value; }
            get { return maxOutput; }
        }

        [HasDefaultValue(0.0f, true)]
        public float Charge
        {
            get { return charge; }
            set 
            {
                if (float.IsNaN(value)) return;
                charge = MathHelper.Clamp(value, 0.0f, capacity); 
            }
        }

        [HasDefaultValue(10.0f, false), Editable]
        public float Capacity
        {
            get { return capacity; }
            set { capacity = Math.Max(value, 1.0f); }
        }

        [HasDefaultValue(10.0f, false), Editable]
        public float RechargeSpeed
        {
            get { return rechargeSpeed; }
            set
            {
                if (float.IsNaN(value)) return;                
                rechargeSpeed = MathHelper.Clamp(value, 0.0f, maxRechargeSpeed);
                rechargeSpeed = MathUtils.Round(rechargeSpeed, Math.Max(maxRechargeSpeed * 0.1f, 1.0f));
            }
        }

        [HasDefaultValue(10.0f, false), Editable]
        public float MaxRechargeSpeed
        {
            get { return maxRechargeSpeed; }
            set { maxRechargeSpeed = Math.Max(value, 1.0f); }
        }

        public PowerContainer(Item item, XElement element)
            : base(item, element)
        {
            //capacity = ToolBox.GetAttributeFloat(element, "capacity", 10.0f);
            //maxRechargeSpeed = ToolBox.GetAttributeFloat(element, "maxinput", 10.0f);
            //maxOutput = ToolBox.GetAttributeFloat(element, "maxoutput", 10.0f);
            
            IsActive = true;
        }

        public override bool Pick(Character picker)
        {
            if (picker == null) return false;

            //picker.SelectedConstruction = (picker.SelectedConstruction == item) ? null : item;
            
            return true;
        }

        public override void Update(float deltaTime, Camera cam) 
        {
            float chargeRate = (float)(Math.Sqrt(charge / capacity));
            //float gridPower = 0.0f;
            float gridLoad = 0.0f;

            //if (item.linkedTo.Count == 0) return;

            foreach (Connection c in item.Connections)
            {
                foreach (Connection c2 in c.Recipients)
                {
                    PowerTransfer pt = c2.Item.GetComponent<PowerTransfer>();
                    if (pt == null) continue;

                    gridLoad += pt.PowerLoad; 
                }
            }


            float gridRate = voltage;

            if (gridRate>minVoltage)
            {
                ApplyStatusEffects(ActionType.OnActive, deltaTime, null);            
            }

            //recharge
            if (gridRate >= chargeRate)
            {
                if (charge >= capacity)
                {
                    currPowerConsumption = 0.0f;
                    charge = capacity;
                    return;
                }

                currPowerConsumption = MathHelper.Lerp(currPowerConsumption, rechargeSpeed, 0.05f);
                charge += currPowerConsumption*voltage / 3600.0f;
            }
            //provide power to the grid
            else if (gridLoad > 0.0f)
            {
                if (charge <= 0.0f)
                {
                    currPowerConsumption = 0.0f;
                    charge = 0.0f;
                    return;
                }

                //currPowerConsumption = MathHelper.Lerp(
                //   currPowerConsumption,
                //   -maxOutput * chargeRate,
                //   0.1f);

                currPowerConsumption = MathHelper.Lerp(
                   currPowerConsumption,
                   -Math.Min(maxOutput * chargeRate, gridLoad - (gridLoad * voltage)),
                   0.05f);

                //powerConsumption = MathHelper.Lerp(
                //    powerConsumption,
                //    -Math.Min(maxOutput * chargeRate, gridLoad - (power)),
                //    0.1f);

                //powerConsumption = Math.Min(powerConsumption, 0.0f);
                charge -= -currPowerConsumption / chargeRate / 3600.0f;
            }

            voltage = 0.0f;
        }

        public override void Draw(SpriteBatch spriteBatch, bool editing)
        {
            base.Draw(spriteBatch);

            GUI.DrawRectangle(spriteBatch,
                new Vector2(item.Rect.X + item.Rect.Width / 2 - 4, -item.Rect.Y + 9),
                new Vector2(8, 22), Color.Black);

            if (charge > 0)
                GUI.DrawRectangle(spriteBatch,
                    new Vector2(item.Rect.X + item.Rect.Width / 2 - 3, -item.Rect.Y + 10 + (20.0f * (1.0f - charge / capacity))),
                    new Vector2(6, 20 * (charge / capacity)), Color.Green, true);
        }

        public override void DrawHUD(SpriteBatch spriteBatch, Character character)
        {
            GuiFrame.Draw(spriteBatch);

            int x = GuiFrame.Rect.X;
            int y = GuiFrame.Rect.Y;
            //GUI.DrawRectangle(spriteBatch, new Rectangle(x, y, width, height), Color.Black, true);

            spriteBatch.DrawString(GUI.Font,
                "Charge: " + (int)charge + "/" + (int)capacity + " (" + (int)((charge / capacity) * 100.0f) + " %)",
                new Vector2(x + 30, y + 30), Color.White);

            spriteBatch.DrawString(GUI.Font, "Recharge rate: " + (int)((rechargeSpeed / maxRechargeSpeed)*100.0f)+" %", new Vector2(x + 30, y + 100), Color.White);
            if (GUI.DrawButton(spriteBatch, new Rectangle(x + 200, y + 90, 40, 40), "+"))
            {
               rechargeSpeed = Math.Min(rechargeSpeed + maxRechargeSpeed*0.1f, maxRechargeSpeed);
               item.NewComponentEvent(this, true);
            }

            if (GUI.DrawButton(spriteBatch, new Rectangle(x + 250, y + 90, 40, 40), "-"))
            {
                rechargeSpeed = Math.Max(rechargeSpeed - maxRechargeSpeed * 0.1f, 0.0f);                
                item.NewComponentEvent(this, true);
            }
        }

        public override void FillNetworkData(Networking.NetworkEventType type, Lidgren.Network.NetOutgoingMessage message)
        {
            message.Write((byte)((int)(rechargeSpeed/maxRechargeSpeed*255.0f)));
            message.Write(charge);
        }

        public override void ReadNetworkData(Networking.NetworkEventType type, Lidgren.Network.NetIncomingMessage message)
        {
            byte newRechargeSpeed = 0;
            float newCharge = 0.0f;

            try
            {
                newRechargeSpeed = message.ReadByte();
                newCharge = message.ReadFloat();
            }
            catch { }

            RechargeSpeed = (newRechargeSpeed/255.0f)*maxRechargeSpeed;
            Charge = newCharge;
        }

    }
}
