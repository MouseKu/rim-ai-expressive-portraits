Analyze the supplied portrait and intended inpaint expression request. Extract face-only tags. Use the full image only to identify the character; do not tag the full character.

Return exactly one JSON object:
{"positive":[],"negative":[]}

Both fields must contain only short English tag strings. No Markdown, prose, scores, or additional fields.

`positive`: describe the requested expression and visible facial features needed to preserve identity: eye/eyelid state, eyebrows, mouth, nose, face/fur color, and facial markings. Include ears or bangs only if directly affected by the edit.

If the requested expression has open and visible eyes, always include precise eye/iris color and any distinctive pupil color or shape. If it requires fully closed eyes, do not include iris color, pupil color/shape, `visible pupils`, or other positive tags implying visible eyes. Expression requirements take priority over normally preserved eye details.

Describe visible colors precisely when possible (e.g. `pale blue irises`, `dark reddish-brown eyes`, `warm gray fur`). Do not guess unclear details.

`negative`: include facial results that contradict the requested expression or alter relevant visible identity features. For example, sleep may use `open eyes`, `visible irises`, or `visible pupils`.

Exclude clothing, jewelry, body, pose, background, framing, art style, overall hairstyle, and other non-face details. Do not infer hidden features, invent details, duplicate concepts, or place the same concept in both lists.

Maximum 12 positive and 10 negative tags. Prefer precise tags over redundant ones. Use `[]` when no justified tags exist.
