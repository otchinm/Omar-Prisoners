using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>
    /// Thin wrapper over the legacy Input Manager. Gameplay reads through here so UI screens can
    /// block gameplay input (inventory, pause, keypad...).
    /// </summary>
    public static class GameInput
    {
        /// <summary>Set by the UI each frame when a modal screen wants exclusive input.</summary>
        public static bool GameplayBlocked;

        public static bool CursorLocked { get; private set; }

        public static void SetCursorLocked(bool locked)
        {
            CursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = false; // the UI draws its own pixel cursor
        }

        static bool Active => !GameplayBlocked;

        public static Vector2 Move
        {
            get
            {
                if (!Active) return Vector2.zero;
                float x = 0, y = 0;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1;
                var v = new Vector2(x, y);
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        /// <summary>Mouse look delta in degrees (sensitivity applied).</summary>
        public static Vector2 Look
        {
            get
            {
                if (!Active || !CursorLocked) return Vector2.zero;
                float s = Settings.MouseSensitivity;
                float y = Input.GetAxisRaw("Mouse Y") * s;
                return new Vector2(Input.GetAxisRaw("Mouse X") * s, Settings.InvertY ? -y : y);
            }
        }

        public static bool Sprint => Active && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        public static bool CrouchPressed => Active && (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl));
        public static bool InteractDown => Active && Input.GetKeyDown(KeyCode.E);
        public static bool InteractHeld => Active && Input.GetKey(KeyCode.E);
        public static bool UseDown => Active && (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(0));
        public static bool PrimaryDown => Active && Input.GetMouseButtonDown(0);
        public static bool SecondaryDown => Active && Input.GetMouseButtonDown(1);
        public static bool ToggleItemDown => Active && Input.GetKeyDown(KeyCode.F);
        public static bool DropDown => Active && Input.GetKeyDown(KeyCode.G);
        public static bool Ability1Down => Active && Input.GetKeyDown(KeyCode.T);
        public static bool Ability2Down => Active && Input.GetKeyDown(KeyCode.G);
        public static bool Ability3Down => Active && Input.GetKeyDown(KeyCode.Q);
        public static float Scroll => Active ? Input.mouseScrollDelta.y : 0f;

        public static int SlotPressed
        {
            get
            {
                if (!Active) return -1;
                if (Input.GetKeyDown(KeyCode.Alpha1)) return 0;
                if (Input.GetKeyDown(KeyCode.Alpha2)) return 1;
                if (Input.GetKeyDown(KeyCode.Alpha3)) return 2;
                if (Input.GetKeyDown(KeyCode.Alpha4)) return 3;   // with a backpack
                if (Input.GetKeyDown(KeyCode.Alpha5)) return 4;
                return -1;
            }
        }

        // UI-level keys (work even when gameplay is blocked)
        public static bool InventoryToggle => Input.GetKeyDown(KeyCode.Tab);
        /// <summary>(iteration 3) The journal of everything read / watched this night.</summary>
        public static bool JournalToggle => Input.GetKeyDown(KeyCode.J);
        public static bool PauseToggle => Input.GetKeyDown(KeyCode.Escape);
    }
}
