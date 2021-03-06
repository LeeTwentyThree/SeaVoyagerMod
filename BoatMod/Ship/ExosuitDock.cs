﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace ShipMod.Ship
{
    public class ExosuitDock : MonoBehaviour
    {
        public ShipBehaviour ship;
        LineRenderer cableRenderer;
        Transform cableConnectionPoint;
        Transform armTransform;
        SkinnedMeshRenderer dynamicCableModel;
        Sprite spriteButtonActive;
        Sprite spriteButtonInactive;
        Button retractButton;
        Button extendButton;
        Image retractButtonImage;
        Image extendButtonImage;
        AudioSource moveSound;
        CableTrigger cableTrigger;

        public Vehicle currentVehicle;

        float buttonNextPressTime;
        /// <summary>
        /// The appearance-wise y position of the cable hook.
        /// </summary>
        float cableLocalY;
        /// <summary>
        /// Whether the arm is hanging over the water or not.
        /// </summary>
        bool dockExtended;
        /// <summary>
        /// The code-wide y position of the cable hook.
        /// </summary>
        float cableTargetLocation;
        CableState cableState;
        const float maxCableDepth = -100f;
        const float maxDivingBellDepth = -400f;
        const float cableSpeed = 10f;

        /// <summary>
        /// Whether the cable is holding an object or not.
        /// </summary>
        public bool Occupied
        {
            get
            {
                return currentVehicle != null;
            }
        }
        /// <summary>
        /// How long the cable is (absolute).
        /// </summary>
        public float CableLength
        {
            get
            {
                return Mathf.Abs(cableTargetLocation);
            }
        }
        /// <summary>
        /// How long the cable can be (absolute).
        /// </summary>
        public float MaxCableLength
        {
            get
            {
                if (PlayerInDivingBell)
                {
                    return Mathf.Abs(maxDivingBellDepth);
                }
                else
                {
                    return Mathf.Abs(maxCableDepth);
                }
            }
        }
        /// <summary>
        /// Where the cable ends code-wise.
        /// </summary>
        public Vector3 CableTargetWorldPosition
        {
            get
            {
                return new Vector3(cableConnectionPoint.position.x, cableConnectionPoint.position.y + cableTargetLocation, cableConnectionPoint.position.z);
            }
        }
        /// <summary>
        /// Where the cable ends appearance-wise.
        /// </summary>
        public Vector3 CableEndWorldPosition
        {
            get
            {
                return new Vector3(cableConnectionPoint.position.x, cableConnectionPoint.position.y + cableLocalY, cableConnectionPoint.position.z);
            }
        }
        /// <summary>
        /// Whether the player is in the prawn suit while it is attached to the ship or not. I call it a "diving bell" because that's essentially what it is in this case.
        /// </summary>
        public bool PlayerInDivingBell
        {
            get
            {
                return Player.main.GetVehicle() == currentVehicle;
            }
        }

        public void Initialize()
        {
            cableRenderer = gameObject.GetComponentInChildren<LineRenderer>();
            dynamicCableModel = gameObject.GetComponentInChildren<SkinnedMeshRenderer>();
            cableConnectionPoint = gameObject.SearchChild("CableTop").transform;
            armTransform = gameObject.SearchChild("DockArm").transform;
            moveSound = gameObject.SearchComponent<AudioSource>("ArmMoveSound");
            moveSound.volume = QPatch.config.AudioVolume;
            cableTrigger = gameObject.SearchChild("CableTrigger").AddComponent<CableTrigger>();
            cableTrigger.dock = this;

            gameObject.SearchComponent<Button>("DockToggleButton").onClick.AddListener(OnDockButton);

            retractButton = gameObject.SearchComponent<Button>("CabelRaiseButton");
            retractButton.onClick.AddListener(OnRetractButton);
            retractButtonImage = retractButton.GetComponent<Image>();

            extendButton = gameObject.SearchComponent<Button>("CableDropButton");
            extendButton.onClick.AddListener(OnExtendButton);
            extendButtonImage = extendButton.GetComponent<Image>();

            spriteButtonActive = QPatch.bundle.LoadAsset<Sprite>("sprite_arrowon.png");
            spriteButtonInactive = QPatch.bundle.LoadAsset<Sprite>("sprite_arrowoff.png");
        }

        void Update()
        {
            switch (cableState)
            {
                case CableState.Retracting:
                    if (cableTargetLocation < -2f)
                    {
                        cableTargetLocation += Time.deltaTime * cableSpeed;
                    }
                    break;
                case CableState.Extending:
                    if (Occupied)
                    {
                        if (Physics.Raycast(CableTargetWorldPosition + (Vector3.down * 2f), Vector3.down, 1.5f))
                        {
                            break;
                        }
                    }
                    else
                    {
                        if (Physics.Raycast(CableTargetWorldPosition, Vector3.down, 3.5f))
                        {
                            break;
                        }
                    }
                    if (cableTargetLocation > -MaxCableLength)
                    {
                        cableTargetLocation -= Time.deltaTime * cableSpeed;
                    }
                    break;
                default:
                    break;
                case CableState.Default:
                    cableTargetLocation = -2f;
                    break;
            }
            if (CableLength >= MaxCableLength)
            {
                extendButtonImage.sprite = spriteButtonInactive;
            }
            if (CableLength <= 1f)
            {
                extendButtonImage.sprite = spriteButtonInactive;
            }
            Quaternion armTargetRotation;
            if (dockExtended)
            {
                armTargetRotation = Quaternion.identity; //The arm faces out over the deck.
            }
            else
            {
                armTargetRotation = Quaternion.Euler(0f, 0f, 90f); //The arm is in "resting position". Technically this is pointless but it's fun for it to be interactable.
            }
            cableLocalY = Mathf.MoveTowards(cableRenderer.GetPosition(1).y, cableTargetLocation, Time.deltaTime * 10f); //Move towards default position
            armTransform.localRotation = Quaternion.RotateTowards(armTransform.localRotation, armTargetRotation, Time.deltaTime * 30f); //Rotate the arm to the correct position, smoothly. I believe this makes it move at the rate of 30 degrees per second.
            cableRenderer.SetPosition(1, new Vector3(0f, cableLocalY, 0f)); //Apply the cableLocalY to the physical cable.
            float tightnessScale = Mathf.Clamp((cableLocalY / -20f * 100f), 0f, 100f);
            dynamicCableModel.SetBlendShapeWeight(0, tightnessScale);
            cableTrigger.transform.position = CableEndWorldPosition;
            if (currentVehicle)
            {
                currentVehicle.transform.position = cableTrigger.transform.position + (Vector3.down * 2f);
                if (currentVehicle is Exosuit)
                {
                    currentVehicle.useRigidbody.isKinematic = true;
                }
                else
                {
                    currentVehicle.useRigidbody.velocity = Vector3.zero;
                }
            }
            var vehicle = Player.main.GetVehicle();
            if (vehicle != null)
            {
                if (currentVehicle == vehicle)
                {
                    ProcessInput();
                }
            }
        }

        void ProcessInput()
        {
            if (GameInput.GetButtonDown(GameInput.Button.MoveUp))
            {
                OnRetractButton();
            }
            if (GameInput.GetButtonDown(GameInput.Button.MoveDown))
            {
                OnExtendButton();
            }
            if (GameInput.GetButtonDown(GameInput.Button.Deconstruct))
            {
                DetatchVehicle();
            }
        }

        void OnDockButton()
        {
            if (Time.time > buttonNextPressTime)
            {
                if (SetDockExtended(!dockExtended))
                {
                    buttonNextPressTime = Time.time + 3f;
                }
            }
        }

        void IgnorePhysicsWithVehicle(Vehicle vehicle, bool state)
        {
            foreach(Collider col in ship.gameObject.GetComponentsInChildren<Collider>())
            {
                foreach(var col2 in vehicle.GetComponentsInChildren<Collider>())
                {
                    if(col2.gameObject.GetComponent<Player>() == null) Physics.IgnoreCollision(col, col2, state);
                }
            }
        }

        public bool SetDockExtended(bool newState)
        {
            if (newState == dockExtended || CableLength > 3f)
            {
                return false;
            }
            if (Occupied && dockExtended)
            {
                DetatchVehicle();
            }
            dockExtended = newState;
            moveSound.Play();
            return true;
        }

        public bool GetCanDock()
        {
            return !Occupied && dockExtended;
        }

        public void AttachVehicle(Vehicle vehicle)
        {
            currentVehicle = vehicle;
            ErrorMessage.AddMessage(string.Format("{0} attached.", new object[] { currentVehicle.GetName()}));
            if(currentVehicle is Exosuit) currentVehicle.useRigidbody.isKinematic = true;
            currentVehicle.gameObject.EnsureComponent<HeldByCable>().dock = this;
            cableState = CableState.Stopped;
            retractButtonImage.sprite = spriteButtonInactive;
            extendButtonImage.sprite = spriteButtonInactive;
            IgnorePhysicsWithVehicle(vehicle, true);
            if (Player.main.GetVehicle() == vehicle)
            {
                QPatch.PrintExoCustomControls();
            }
        }

        public void DetatchVehicle()
        {
            if(currentVehicle != null)
            {
                if(currentVehicle is Exosuit) currentVehicle.useRigidbody.isKinematic = false;
                currentVehicle.gameObject.EnsureComponent<HeldByCable>().dock = null;
                IgnorePhysicsWithVehicle(currentVehicle, false);
                currentVehicle = null;
            }
        }

        void OnRetractButton()
        {
            if (CableLength > 1f)
            {
                cableState = CableState.Retracting;
                retractButton.GetComponent<Image>().sprite = spriteButtonActive;
                extendButton.GetComponent<Image>().sprite = spriteButtonInactive;
            }
        }
        void OnExtendButton()
        {
            if (dockExtended && CableLength < MaxCableLength)
            {
                cableState = CableState.Extending;
                retractButton.GetComponent<Image>().sprite = spriteButtonInactive;
                extendButton.GetComponent<Image>().sprite = spriteButtonActive;
            }
        }
    }
    public enum CableState
    {
        Default,
        Extending,
        Retracting,
        Stopped
    }
}
