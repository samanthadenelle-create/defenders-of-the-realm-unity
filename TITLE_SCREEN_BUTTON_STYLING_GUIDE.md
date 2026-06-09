# Title Screen Button Styling Guide — Match Epic Fantasy Aesthetic

**Goal:** Buttons (`Play Intro`, `Start New`, `Continue`) match the golden, mystical, epic aesthetic of the title screen.

---

## Visual Reference

**Current page aesthetic:**
- **Color scheme:** Gold (255, 215, 0 / #FFD700), deep forest green, black shadows
- **Typography:** Serif, elegant capitals (title font)
- **Mood:** Epic, mystical, high-fantasy
- **Effects:** Glow, light rays, particle effects
- **Overall:** AAA game production quality

**Current buttons:**
- ❌ Too plain (dark brown, no character)
- ❌ Don't match the epic gold/mystical theme
- ❌ Generic, not tied to game aesthetic

**Target:** Buttons that feel like they belong in Elarion, not a generic UI.

---

## Button Styling Spec

### Visual Design

#### Base Button Style

**Color palette:**
- **Primary:** Gold #D4AF37 (rich, warm — matches title)
- **Secondary:** Dark bronze #3E2723 (button base)
- **Text:** Cream #F5DEB3 (readable on dark, matches gold theme)
- **Hover:** Brighter gold #FFD700 (interactive feedback)
- **Pressed:** Darker bronze #2C1810 (depth)

#### Button Appearance

```
┌─────────────────────────────────────┐
│  [ORNATE BORDER] — Gold outline     │
│                                     │
│   ⚔️  PLAY INTRO  ⚔️                │  ← Serif font, caps
│                                     │
│  [GLOW/SHADOW] — Dark gradient      │
└─────────────────────────────────────┘
```

**Button features:**
1. **Border:** Ornate gold frame (think medieval manuscript borders)
   - Double-line gold border (outer: bright, inner: slightly darker)
   - Corner ornaments (small decorative flourishes)
   - Width: 2–3 pixels

2. **Background:** Dark bronze-black gradient
   - Top: Slightly lighter (#4A3728)
   - Bottom: Darker (#2C1810)
   - Creates depth, makes gold border pop

3. **Text:**
   - Font: Serif (Cinzel, IM Fell English, Cardo, or similar)
   - Style: ALL CAPS
   - Color: Cream/gold (#F5DEB3)
   - Size: ~24–32 px (readable but not huge)
   - Weight: Bold
   - Shadow: Subtle dark drop shadow (1–2 px, #000000, opacity 0.5)

4. **Glow effect:**
   - **Idle:** Faint outer glow (gold, opacity 0.3, blur 8–10 px)
   - **Hover:** Brighter glow (gold, opacity 0.6, blur 15 px)
   - **Pressed:** Glow contracts inward (depth effect)
   - Uses box-shadow (CSS) or glow material (Unity)

5. **Icon/Ornament (optional):**
   - Small sword or shield symbol on left/right of text
   - Gold color, matches text
   - Size: ~16 px
   - Example: `⚔️ PLAY INTRO ⚔️`

---

## Implementation Options

### Option A: CSS (Web/HTML Title Screen)

```css
.title-button {
  /* Layout */
  padding: 16px 32px;
  margin: 12px;
  border-radius: 4px;
  
  /* Border */
  border: 3px solid #D4AF37;
  box-shadow: 
    0 0 0 1px #8B7355,  /* Inner darker border */
    0 0 20px rgba(212, 175, 55, 0.4),  /* Glow */
    inset 0 1px 0 rgba(255, 255, 255, 0.1);  /* Subtle highlight */
  
  /* Background */
  background: linear-gradient(180deg, #4A3728 0%, #2C1810 100%);
  
  /* Text */
  font-family: 'Cinzel', 'IM Fell English', serif;
  font-size: 24px;
  font-weight: bold;
  color: #F5DEB3;
  text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.7);
  text-transform: uppercase;
  letter-spacing: 2px;
  
  /* Interaction */
  cursor: pointer;
  transition: all 0.3s ease;
}

.title-button:hover {
  background: linear-gradient(180deg, #5A4738 0%, #3C2810 100%);
  border-color: #FFD700;
  box-shadow: 
    0 0 0 1px #8B7355,
    0 0 30px rgba(255, 215, 0, 0.8),  /* Brighter glow */
    inset 0 1px 0 rgba(255, 255, 255, 0.2);
  transform: translateY(-2px);  /* Slight lift effect */
}

.title-button:active {
  background: linear-gradient(180deg, #3C3728 0%, #1C1810 100%);
  border-color: #D4AF37;
  box-shadow: 
    0 0 0 1px #8B7355,
    0 0 15px rgba(212, 175, 55, 0.4),
    inset 0 2px 4px rgba(0, 0, 0, 0.5);  /* Pressed depth */
  transform: translateY(0);
}

.title-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  box-shadow: 
    0 0 0 1px #8B7355,
    0 0 10px rgba(212, 175, 55, 0.2);
}
```

**Optional ornament:**
```css
.title-button::before {
  content: "⚔️";
  margin-right: 8px;
}

.title-button::after {
  content: "⚔️";
  margin-left: 8px;
}
```

### Option B: Unity UI

**Canvas button with styling:**

1. **Button component:**
   - Normal color: #D4AF37 (gold)
   - Highlighted color: #FFD700 (bright gold)
   - Pressed color: #3E2723 (bronze)
   - Disabled color: #8B7355 (muted gold)

2. **Image component (background):**
   - Source image: Dark bronze-to-black gradient (create in image editor)
   - Color: White (image handles tone)
   - Type: Sliced (for scaling)
   - Border: Set to create 9-slice frame

3. **Outline component:**
   - Enabled: Yes
   - Color: #D4AF37 (gold)
   - Distance: 1–2 px

4. **Shadow component (optional):**
   - Enabled: Yes
   - Effect color: #000000, opacity 0.5
   - Distance: 2–3 px

5. **Text component (child):**
   - Font: Serif (Cinzel or equivalent)
   - Font size: 32–40
   - Color: #F5DEB3 (cream)
   - Style: Bold
   - Alignment: Center
   - Text: "PLAY INTRO" (all caps)

6. **Glow effect (VFX):**
   - Use particle system or shader to add outer glow
   - Gold color, opacity 0.4–0.6
   - Increases on hover (via animation or script)

---

## Button Layout

### Position & Spacing

**Buttons should be:**
- Centered horizontally on screen
- Near bottom (lower third)
- Spaced evenly apart (36–48 px between centers)
- Same vertical alignment

```
┌─────────────────────────────────────┐
│                                     │
│    (Golden title background)        │
│                                     │
│         ECHOES OF ELARION           │
│                                     │
│    (Forest/particle background)     │
│                                     │
│  [PLAY INTRO]  [START NEW]  [CONT.] │  ← Centered group
│                                     │
│              [Development Build]    │
└─────────────────────────────────────┘
```

---

## Interaction States

### Idle (Default)
- Glow: Soft, subtle (opacity 0.3)
- Color: Gold #D4AF37
- Scale: 1.0
- Cursor: Pointer (hand icon)

### Hover (Mouse over)
- Glow: Bright, visible (opacity 0.6–0.8)
- Color: Bright gold #FFD700
- Scale: 1.05 (slight grow)
- Transition: 0.2 seconds
- Effect: "This button is interactive"

### Pressed (Clicked)
- Glow: Dim (pulled inward)
- Color: Pressed gold #B8950C
- Scale: 0.98 (slight shrink)
- Transition: 0.1 seconds
- Effect: "Button is being clicked"

### Disabled (Grayed out)
- Glow: Very faint (opacity 0.1)
- Color: Muted #8B7355
- Scale: 1.0
- Cursor: Not-allowed (circle-slash)
- Effect: "This button is unavailable"

---

## Typography Details

**Font choice:**
- Primary: Cinzel (Google Fonts, elegant serif)
- Fallback: IM Fell English, Cardo, Georgia (serif stack)
- Why: Matches epic fantasy aesthetic (Game of Thrones, D&D)

**Text rendering:**
- Letter spacing: +2–3 px (epic, spread out)
- Line height: 1.2 (tight, no extra space)
- Case: ALL CAPS (regal, commanding)
- Weight: Bold (700–900)

**Text shadow:**
- Offset: 2 px right, 2 px down
- Blur: 4 px
- Color: #000000
- Opacity: 0.7
- Effect: Readable on any background

---

## Color Reference

| Element | Hex | RGB | Use |
|---|---|---|---|
| Gold (primary) | #D4AF37 | (212, 175, 55) | Borders, glow base |
| Gold (bright) | #FFD700 | (255, 215, 0) | Hover state, highlights |
| Gold (muted) | #8B7355 | (139, 115, 85) | Disabled state |
| Bronze (dark) | #3E2723 | (62, 39, 35) | Button base |
| Bronze (lighter) | #4A3728 | (74, 55, 40) | Gradient top |
| Bronze (darkest) | #2C1810 | (44, 24, 16) | Gradient bottom, depth |
| Text (cream) | #F5DEB3 | (245, 222, 179) | Button text |
| Shadow | #000000 | (0, 0, 0) | Drop shadow, depth |

---

## Animation / Transition

**Smooth interactions:**

```css
/* All properties fade smoothly */
transition: all 0.3s cubic-bezier(0.25, 0.46, 0.45, 0.94);
```

**Timing:**
- Hover effect: 0.2–0.3 seconds (feels responsive)
- Press effect: 0.1 seconds (quick, snappy)
- Glow pulse (optional): 2 seconds loop (ambient feel)

**Optional pulsing glow (idle):**
```css
@keyframes glow-pulse {
  0% { box-shadow: 0 0 20px rgba(212, 175, 55, 0.3); }
  50% { box-shadow: 0 0 30px rgba(212, 175, 55, 0.5); }
  100% { box-shadow: 0 0 20px rgba(212, 175, 55, 0.3); }
}

.title-button {
  animation: glow-pulse 3s ease-in-out infinite;
}
```

---

## Accessibility

**WCAG AA Compliance:**

- **Color contrast:** Gold on bronze passes (4.5:1+)
- **Font size:** 24 px+ (readable)
- **Hover state:** Distinct visual feedback
- **Disabled state:** Visually different
- **Keyboard navigation:** Tab focus visible (gold border becomes brighter)
- **Touch targets:** 48+ px tall (mobile friendly)

```css
/* Focus state for keyboard users */
.title-button:focus {
  outline: 3px solid #FFD700;
  outline-offset: 2px;
}
```

---

## Testing Checklist

- [ ] Gold borders are crisp (not blurry)
- [ ] Glow effect is visible on dark background
- [ ] Text is readable (good contrast)
- [ ] Hover effect is smooth (no jumping)
- [ ] Pressed state shows depth (visual feedback)
- [ ] Disabled state is clearly different
- [ ] Works on mobile (touch targets are large)
- [ ] Works on different screen sizes (responsive)
- [ ] Animation is smooth (60 FPS, not laggy)
- [ ] No flickering or jank

---

## Deliverables Needed

When you provide the new title screen image:

1. **Background image** (whatever resolution fits)
2. **Button styling** (use spec above)
3. **Confirm color palette** (gold, bronze, cream — match screenshot)
4. **Confirm ornament style** (swords, shields, no ornaments?)

Then buttons can be styled to match perfectly.

---

## Final Look

```
         ╔════════════════════════════╗
         ║  DEFENDERS OF THE REALM    ║
         ║   ECHOES OF ELARION        ║
         ╚════════════════════════════╝
         
         [Forest glow background]
         
     ┏━━━━━━━━━━━┓  ┏━━━━━━━━━┓  ┏━━━━━━━━┓
     ┃⚔ PLAY ⚔  ┃  ┃START NEW┃  ┃CONTINUE┃
     ┗━━━━━━━━━━━┛  ┗━━━━━━━━━┛  ┗━━━━━━━━┛
     
         Golden glow, epic feel
         Matches title aesthetic
         AAA game production quality
```

---

## Notes

- All colors are adjustable (tweak hex values if needed)
- Glow intensity can be dialed up/down
- Button size/padding can be adjusted for mobile
- Ornaments (⚔️) are optional (can be removed)
- Font must be serif (fantasy aesthetic requirement)

Ready to style! 🎭✨
