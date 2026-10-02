"""Character texture atlas layout.

MUST stay in sync with Assets/PrisonersOfOmar/Scripts/Characters/CharacterAtlas.cs.

Every character texture is 256x256 RGBA. Rects are (x, y, w, h) in pixels, origin TOP-LEFT of the PNG
(Unity UV: u = x / 256, v = 1 - y / 256). Alpha is 255 everywhere except the cutout regions
(HAIR, EXTRA, MISC) which the mesh renders with the double-sided alpha-tested material.

Cylindrical strips (HEAD, TORSO, ARM, LEG): u goes around the body part, u = 0 / 1 at the back,
u = 0.5 at the front center, the character's RIGHT side (+X) at u < 0.5 so the texture reads correctly
when seen from the front. v goes from the bottom ring (0) to the top ring (1).
"""

SIZE = 256

HEAD = (0, 0, 128, 96)      # neck + head wrap (front-weighted u, see HEAD_U)
TORSO = (128, 0, 128, 112)  # crotch .. neck base
LEG = (0, 96, 64, 128)      # ankle .. hip (both legs, mirrored)
ARM = (64, 96, 48, 112)     # wrist .. shoulder (both arms, mirrored)
ROPE = (112, 96, 16, 112)   # twisted rope strip (noose, hanging rope), v along the rope
HAND = (64, 208, 32, 32)    # back of hand / palm: u across, v = 1 at the wrist, 0 at the finger tips
SWATCH = (96, 208, 32, 48)  # 16x16 flat color cells, see SWATCH_CELLS
FOOT = (0, 224, 64, 32)     # left half: shoe side profile (u heel->toe, v sole->top); right half: top (u across, v toe->ankle)
HAIR = (128, 112, 64, 64)   # cutout: hair strands (v along the strand, root at v = 1)
EXTRA = (192, 112, 64, 144) # cutout: long hair panel (prisoners 2/3) or Omar's apron, top at v = 1
MISC = (128, 176, 64, 80)   # cutout: top 64x24 = glasses front; below (64x56) = Omar's sack skirt / bangs / tufts

SWATCH_CELLS = {  # (col, row) of 16x16 cells inside SWATCH
    "dark": (0, 0),    # glasses frames, eye holes, soles
    "white": (1, 0),   # teeth, eye whites
    "metal": (0, 1),   # buckles, rings
    "blood": (1, 1),
    "skin": (0, 2),
    "hair": (1, 2),
}

# --- tube ring tables (shared with C# BodyMeshGenerator) ---------------------------------------------
# Head tube: 12 sides. Vertex i sits at angle theta_i = 180 - 30 * i degrees (0 = front, +90 = character's right).
HEAD_SIDES = 12
HEAD_U = [0.0, 0.045, 0.09, 0.16, 0.27, 0.385, 0.5, 0.615, 0.73, 0.84, 0.91, 0.955, 1.0]
# Head rings: (y_rel, v). y_rel in head units: chin = 0, crown = 1, negative = neck (neck base = -0.5).
HEAD_RINGS = [(-0.50, 0.00), (-0.22, 0.06), (0.00, 0.12), (0.17, 0.27), (0.31, 0.393), (0.45, 0.516),
              (0.58, 0.63), (0.74, 0.77), (0.88, 0.894), (1.00, 1.0)]

TORSO_SIDES = 12
# torso rings: t = fraction crotch (0) .. neck base (1); v = t
TORSO_T = [0.0, 0.12, 0.26, 0.40, 0.55, 0.68, 0.80, 0.93, 1.0]
LEG_SIDES = 8
LEG_T = [0.0, 0.12, 0.30, 0.42, 0.50, 0.58, 0.75, 0.90, 1.0]   # ankle .. hip
ARM_SIDES = 8
ARM_T = [0.0, 0.20, 0.35, 0.50, 0.65, 0.82, 1.0]             # wrist .. shoulder

# Face landmarks in head space (theta degrees, y_rel) - the mesh puts the glasses / eye holes here.
EYE_THETA = 21.0
EYE_Y = 0.45
MOUTH_Y = 0.17
NOSE_Y = 0.31


def head_theta_from_u(u):
    """Inverse of HEAD_U (piecewise linear). Returns theta in degrees (180 .. -180)."""
    import numpy as np
    thetas = [180 - 30 * i for i in range(HEAD_SIDES + 1)]
    return np.interp(u, HEAD_U, thetas)


def head_u_from_theta(theta):
    import numpy as np
    thetas = [180 - 30 * i for i in range(HEAD_SIDES + 1)]
    # np.interp needs increasing x
    return np.interp(-np.asarray(theta, dtype=np.float64), [-t for t in thetas], HEAD_U)


def head_y_from_v(v):
    import numpy as np
    return np.interp(v, [r[1] for r in HEAD_RINGS], [r[0] for r in HEAD_RINGS])


def head_v_from_y(y):
    import numpy as np
    return np.interp(y, [r[0] for r in HEAD_RINGS], [r[1] for r in HEAD_RINGS])
