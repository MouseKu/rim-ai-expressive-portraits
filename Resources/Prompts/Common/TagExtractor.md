Analyze the supplied character image and Base portrait request. Extract compact image-generation tags describing the character's visible identity.

Return exactly: {"positive":[],"negative":[]}

Use only short English tag strings. No Markdown, prose, scores, or additional fields.

For positive, extract identity-defining visual traits: species features, eye/iris color, hair color and hairstyle, bangs, skin/fur color, facial markings, clothing, accessories, distinctive details, and the explicitly requested expression.

Treat the supplied Features: block as authoritative for visual facts such as gender, biological age, race, xenotype, body type, colors, hair, and apparel. If the sprite is ambiguous or conflicts with supplied data, prefer the supplied data. Use the image for details not supplied. Health conditions are context only; tag their visible effects when supported by the image.

Convert biological age to a broad visual age tag (e.g. young adult), never the number. Do not infer age or gender if absent. Convert hex/RGB values to precise visual color tags rather than raw codes. Preserve useful distinctions such as light/dark, warm/cool, muted/vivid, and mixed hues.

Preserve distinctive species features, hairstyle, bangs, eyes, clothing, patterns, trims, markings, and accessories, including their visible shape and color when reliable.

For negative, include unwanted changes to identity or expression: missing/extra species features, wrong colors, missing distinctive details, wrong eye state, extra anatomy, text, or artifacts. Do not duplicate positive concepts.

Do not infer hidden or unclear details or invent features. Exclude nonvisual health information, traits, backstory, personality, setting, and unseen body/clothing details.

Ignore the source sprite's presentation. Do not tag view/camera angle, gaze direction, orientation, pose, composition, framing, background, or art style unless explicitly required by the Base portrait request.

Maximum 20 positive and 15 negative tags. Prefer specific identity traits over generic details. Remove duplicates and empty tags; use [] when none are justified.