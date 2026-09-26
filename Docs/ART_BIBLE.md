# Art Bible — QuickChecks

## Visual Identity

**One-line brief**: Mini Motorways meets billiards — minimal, geometric, motion-first.

**Reference points**:
- Mini Motorways (Nintendo / Apple Arcade)
- Data Wing
- Tomb of the Mask (for swipe input feel)
- Alto's Odyssey (for visual restraint)

---

## Color Palette

```
Background      #0E1414    Deep dark teal
Track surface   #1B2A2A    Slightly lighter
Track border    #3FE0C2    Cyan accent (signature)
Kart player     #FFD166    Warm yellow
Ghost kart      #7B8A8A    Muted gray-blue, 60% alpha
Power-up boost  #FF6B6B    Coral red
Power-up shield #4ECDC4    Mint
Power-up magnet #C7A3FF    Lavender
Power-up phase  #FFB627    Amber
Power-up rewind #6BCB77    Green
Power-up slingshot #B983FF Purple
UI text         #F7F7F2    Off-white
UI background   #161C1C    Slightly lighter than bg
UI accent       #3FE0C2    Cyan (matches track border)
```

---

## Typography

- **Display / numbers**: Inter Bold (or Manrope Bold)
- **Body / labels**: Inter Regular
- **Mono / timers**: JetBrains Mono (for race timer, finish times)
- **Size scale**: 12 / 14 / 18 / 24 / 36 / 64

---

## Sprite Style

- **Karts**: Simple top-down silhouettes, ~64×64 px, single color with darker outline
- **Tracks**: Wide flat-color fills with thin border lines, no textures
- **Power-ups**: Geometric shapes (circle, hexagon, triangle) with category color
- **Particles**: Single dots, no smoke or complex effects
- **Decorations**: Sparse — occasional geometric shapes on track sides for visual interest

---

## Animation Principles

1. **Squash & stretch** on swipe impact (kart compresses briefly in direction of swipe)
2. **Anticipation**: pre-race countdown ticks cause kart to pulse
3. **Follow-through**: kart wobble after sharp turn (settle over 0.3s)
4. **Slow in/out**: ease curves on all camera transitions
5. **Exaggeration**: power-up pickup = particle burst 2x larger than feels "right"
6. **Solid drawing**: karts have subtle drop shadow for depth (no fake 3D, just visual weight)

---

## Anti-patterns (avoid these)

- ❌ Textures, gradients, drop shadows on UI
- ❌ Realistic physics wobble (karts should feel snappy, not floaty)
- ❌ Busy backgrounds that compete with the track
- ❌ Particle effects that last >0.5s
- ❌ Multiple competing accent colors in one scene
- ❌ Realistic car silhouettes — keep it geometric

---

*End of art bible. Update when palette or style is locked.*
