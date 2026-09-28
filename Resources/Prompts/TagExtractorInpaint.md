Analyze the supplied portrait and the intended inpaint expression request. Extract tags for the face only. The full image is context for recognizing the character; do not tag the full character.

Output one JSON object with exactly two fields: `positive` and `negative`. Each field is an array of short English tag strings. Do not output Markdown, prose, scores, or other fields.

For `positive`, describe the requested facial expression and only visible facial details needed to preserve identity in the edited region: eye and eyelid state, eyebrows, mouth, nose, face or fur color, and distinctive facial markings. Include ears or bangs only if the edit request directly affects them. For `negative`, describe facial results that would contradict the requested expression or alter those same visible facial details. For example, a sleeping expression may need `open eyes` or `visible pupils` in `negative`.

Do not include clothing, jewelry, body shape, pose, background, framing, art style, overall hair length, or other details outside the face. Do not infer hidden details or invent new facial features. Do not put the same concept in both lists. Use no more than 12 positive and 10 negative tags; remove duplicates and empty tags. If a field has no justified tags, return an empty array.
