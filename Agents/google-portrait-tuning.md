# Gemini Base Prompt Tuning — Test4

## 1. Objective

Find a **shared Base Prompt** for `gemini-3.1-flash-lite-image` that works across different characters.

### Constraints

- Input: one character image + one shared Base Prompt.

- Do not manually describe character-specific eye colors, clothing colors, accessories, etc.

- Target images are used only for visual comparison and are **not sent to the API**.

- Output: `1:1`, `1024×1024`.

- Background: flat pink.

- Goal: preserve character identity while producing a hand-drawn bust portrait suitable for RimWorld.

---

## 2. Main Finding

Gemini can preserve a substantial amount of character identity **directly from the source image without explicitly listing character-specific traits in text**.

The most effective generic preservation instructions were:

```text
Preserve the actual hair shape and color,
face, ears or other species features,
outfit layers, trim, and accessories.

Keep the original light-versus-dark arrangement
of the clothes, including sleeves, bodice,
and apron if present.
```

The key discovery was that preserving the **relative light/dark structure of clothing** worked better than trying to generically preserve individual clothing colors.

`006` is currently the best Base Prompt candidate. Re-running the same prompt as `009` produced similarly good results, suggesting that the result was not merely a single lucky generation.

---

## 3. Experiment Progression

### 001 → 002: Fixing the Chibi Look

**001 problems**

- Character identity and composition were mostly preserved.

- Eyes were too large.

- Strong chibi/mascot appearance.

**Changes in 002**

```text
natural portrait proportions
not a chibi mascot
modestly sized eyes
subtle brows/nose/mouth
hand-drawn indie game portrait
```

Also discouraged overly smooth vector/chibi rendering.

**Result**

- Better facial proportions.

- Black sleeves and white apron preserved well.

- Style moved closer to the target.

**Lesson**

`2D game portrait` alone is too vague. Gemini tends to interpret it as a chibi, mascot, or sticker-like illustration.

---

### 002 → 003: Removing Character-Specific Restrictions

002 contained a restriction targeting a particular accessory error.

It was useful for that specific character but unsuitable for a shared Base Prompt, so it was removed in 003.

**Lesson**

Do not add character-specific mistakes to the global Base Prompt as negative instructions.

A restriction that fixes one character may suppress valid features on another.

---

### 003–005: Clothing Color Preservation Failure

A recurring failure appeared:

```text
black sleeves → white/light sleeves
```

Simple instructions such as:

```text
preserve sleeve colors
preserve garment layers
```

were not reliable enough.

This became one of the most important observations of the test.

---

### 005: Improving the Hand-Drawn Style

Earlier outputs were too:

- flat

- simple

- vector-like

The prompt was expanded with concrete visual characteristics:

```text
expressive dark brown ink lines
varied line weight
slightly imperfect hand-drawn contours
layered tapered hair locks
fabric folds / seams / trim
controlled asymmetry
human drawing decisions
```

And discouraged:

```text
vector clip art
mascot stickers
generic chibi heads
smooth geometric circles
plastic gloss
```

**Result**

- Better layered hair.

- More line-weight variation.

- More clothing folds and structure.

- Stronger hand-drawn game illustration appearance.

**Lesson**

Describing **observable drawing characteristics** works better than relying on a style name alone.

---

### 005 → 006: Solving Clothing Preservation

005 improved the art style but made the clothing too bright.

006 introduced:

```text
Keep the original light-versus-dark arrangement
of the clothes
```

along with:

```text
warm muted palette
restrained shading
limited highlights
no airbrushed skin
```

**Result**

- Black sleeves remained dark.

- White apron remained light.

- Clothing structure stayed relatively stable.

- Hand-drawn style remained strong.

**Key Finding**

For a generic prompt that does not know exact color names, preserving the **relative brightness relationships between clothing regions** appears more reliable than repeatedly asking the model to preserve individual colors.

006 produced the best overall balance between:

```text
identity preservation
+
clothing preservation
+
hand-drawn style
+
composition
```

---

### 007: Too Much Style Control

Additional restrictions were introduced:

```text
base color + one shadow
matte hair
almost no catchlights
tiny relaxed mouth
```

The visual style moved closer to the target, but composition degraded.

The camera moved farther away and showed the character down to approximately the waist.

**Lesson**

Increasing style-specific constraints can reduce compliance with other requirements, especially composition.

006 appears to be a better stopping point.

---

### 008: Stronger Composition Constraints

To fix the distant framing from 007, 008 added explicit composition rules such as:

```text
head = top 60%
crop above waist
no waist / hips / hands
show lower chest
```

**Result**

- Composition improved.

- Clothing identity became less stable.

- The previously white apron became darker.

**Lesson**

Overly numerical composition constraints do not necessarily improve the overall result.

There appears to be a trade-off between aggressive composition control and identity preservation.

---

### 009: Reproduction Test

009 reused the exact 006 prompt.

It again produced:

- appropriate hand-drawn style

- black sleeves

- white apron

- reasonable bust framing

This suggests that the success of 006 was **not purely a one-seed accident**.

However, some clothing and composition variation remained.

**Conclusion**

Prompt quality improved substantially, but Gemini's inherent generation randomness still matters.

---

## 4. Background Handling

Initially, the prompt requested an exact background:

```text
every pixel = #E69EB1
```

This was unnecessarily strict.

Because the background is removed during post-processing, the requirement was relaxed to:

```text
simple, roughly uniform pink #E69EB1 background
```

**Lesson**

Do not spend excessive prompt complexity trying to enforce pixel-perfect background color.

A sufficiently uniform removable background is enough.

---

## 5. Current Best Base Prompt Structure

`006` / `009` is the current baseline.

Its important components are:

### Identity

Treat the source image as the authoritative source of character identity and design.

### Appearance Preservation

Preserve:

```text
hair shape/color
face
species traits
clothing structure
clothing layers
trim
accessories
relative light/dark clothing relationships
```

### Style

Use:

```text
dark-brown varied ink lines
slightly imperfect hand-drawn contours
layered tapered hair locks
restrained cel shading
fabric folds / seams / trim
warm muted palette
controlled asymmetry
```

Avoid:

```text
vector-like rendering
chibi/mascot appearance
plastic gloss
overly smooth geometric forms
```

### Composition

Target:

```text
three-quarter bust
full head visible
ears/horns/headwear visible
shoulders + meaningful chest area
torso continuing beyond bottom edge
```

---

## 6. Approaches That Were Weak or Counterproductive

Avoid relying heavily on:

- Abstract style labels such as `2D game portrait`.

- Character-specific error restrictions in the shared Base Prompt.

- Repeatedly adding instructions like `preserve sleeve color`.

- Excessively detailed shading restrictions.

- Exact canvas percentages for composition.

- Pixel-perfect RGB background requirements.

- Judging a prompt from only one successful or failed generation.

---

## 7. Important Difference from NAI V4.5

In the previous NAI V4.5 tests, detailed clothing identity was difficult to preserve unless character-specific traits were explicitly described in the prompt.

Gemini behaved differently.

A relatively generic combination of:

```text
source image = authoritative identity/design reference
+
preserve clothing structure
+
preserve relative light/dark relationships
```

was sufficient to retain a substantial amount of character detail.

This makes Gemini potentially better suited to a **generic BaseProvider architecture**, where the same Base Prompt must work without generating a detailed character-specific description first.

This is **not yet proven across multiple characters**.

---

## 8. Recommended Next Experiment

Do **not** make 006 substantially longer yet.

The next priority should be testing the unchanged 006 prompt across diverse characters.

### Character Coverage

Test characters with:

```text
different hair colors
different skin colors
human characters
animal ears
horns
bright clothing
dark clothing
complex layered clothing
many accessories
```

### Evaluation Criteria

For each generation, check:

```text
species traits
hair shape/color
face identity
clothing layers
relative clothing colors
accessories
portrait crop
style consistency
background removability
```

Multiple generations per character are preferable because Gemini still shows stochastic variation.

---

## 9. Final Takeaway

> Gemini can preserve substantial character identity directly from the source image without requiring character-specific textual descriptions.

The most important prompt-engineering discovery was:

```text
Instead of:
"Preserve each clothing color."

Prefer:
"Preserve the original light-versus-dark arrangement of the clothes."
```

This generic relative-appearance instruction produced better clothing identity preservation without requiring knowledge of the character's exact colors.

**006 / 009 is the current best shared Base Prompt baseline.**

The next step should be **cross-character validation rather than further prompt expansion**.
