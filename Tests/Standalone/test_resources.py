"""Resource contracts that can be checked without RimWorld or network access."""

import collections
import json
import re
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
RESOURCES = ROOT / "Resources"
SOURCE = ROOT / "Source" / "RimAIPortrait"


def load_json(path):
    def unique_keys(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("Duplicate JSON key: " + key)
            result[key] = value
        return result

    return json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=unique_keys)


def load_translations(language):
    path = RESOURCES / "Languages" / language / "Keyed" / "RimAIPortrait.xml"
    entries = list(ET.parse(path).getroot())
    names = [entry.tag for entry in entries]
    duplicates = [name for name, count in collections.Counter(names).items() if count > 1]
    if duplicates:
        raise ValueError("Duplicate translation keys in " + language + ": " + ", ".join(duplicates))
    return {entry.tag: entry.text or "" for entry in entries}


class ResourceTests(unittest.TestCase):
    def test_json_files_are_objects_without_duplicate_keys(self):
        files = sorted((RESOURCES / "Json").glob("*.json"))
        self.assertTrue(files)
        for path in files:
            with self.subTest(file=path.name):
                self.assertIsInstance(load_json(path), dict)

    def test_default_values_match_settings_fields_and_types(self):
        source = (SOURCE / "Settings" / "Settings.cs").read_text(encoding="utf-8-sig")
        source = source.split("public PortraitSettings()", 1)[0]
        fields = {}
        for field_type, declaration in re.findall(
            r"\bpublic\s+(bool|int|string|float|AiProvider|TagExtractorModel)\s+([^;]+);", source
        ):
            for part in declaration.split(","):
                fields[part.split("=")[0].strip()] = field_type

        expected_types = {
            "bool": bool,
            "int": int,
            "string": str,
            "float": (int, float),
            "AiProvider": int,
            "TagExtractorModel": int,
        }
        for name, value in load_json(RESOURCES / "Json" / "default-values.json").items():
            with self.subTest(setting=name):
                self.assertIn(name, fields, "JsonUtility would ignore this unknown field")
                self.assertIsInstance(value, expected_types[fields[name]])
                if fields[name] == "float":
                    self.assertNotIsInstance(value, bool)

    def test_translations_have_matching_keys_and_placeholders(self):
        english = load_translations("English")
        korean = load_translations("Korean")
        self.assertEqual(set(english), set(korean))
        for key in english:
            with self.subTest(key=key):
                self.assertTrue(english[key].strip())
                self.assertTrue(korean[key].strip())
                self.assertEqual(
                    set(re.findall(r"\{(\d+)\}", english[key])),
                    set(re.findall(r"\{(\d+)\}", korean[key])),
                )

    def test_literal_localization_keys_used_by_code_exist(self):
        translations = load_translations("English")
        pattern = re.compile(r'Localization\.T\(\s*"([^"]+)"\s*[,)]')
        for path in SOURCE.rglob("*.cs"):
            for key in pattern.findall(path.read_text(encoding="utf-8-sig")):
                with self.subTest(file=path.name, key=key):
                    self.assertIn(key, translations)

    def test_each_portrait_state_has_a_prompt_and_translations(self):
        source = (SOURCE / "Portraits" / "PromptLibrary.cs").read_text(encoding="utf-8-sig")
        match = re.search(r"public enum PortraitState\s*\{([^}]+)\}", source)
        self.assertIsNotNone(match)
        states = [state.strip() for state in match.group(1).split(",")]
        english = load_translations("English")
        korean = load_translations("Korean")
        for provider in ("OpenAI", "Google", "Comfy", "NAI"):
            for name in ("BasePrompt.md", "ExpressionPrompt.md") + tuple(
                state + ".md" for state in states if state != "Base"
            ):
                with self.subTest(provider=provider, prompt=name):
                    prompt = RESOURCES / "Prompts" / provider / name
                    self.assertTrue(prompt.is_file())
                    if (provider, name) != ("Comfy", "BasePrompt.md"):
                        self.assertTrue(prompt.read_text(encoding="utf-8-sig").strip())
        for name in ("TagExtractor.md", "TagExtractorInpaint.md"):
            with self.subTest(prompt=name):
                prompt = RESOURCES / "Prompts" / "Common" / name
                self.assertTrue(prompt.is_file())
                self.assertTrue(prompt.read_text(encoding="utf-8-sig").strip())
        for state in states:
            self.assertIn("RimAIPortrait.State." + state, english)
            self.assertIn("RimAIPortrait.State." + state, korean)

    def test_image_captions_have_required_entries(self):
        required = {"characterSprite", "basePortrait", "selectedReference", "styleReference", "priority"}
        for name in ("open-ai-img-caption.json", "google-ai-img-caption.json"):
            with self.subTest(file=name):
                captions = load_json(RESOURCES / "Json" / name)
                self.assertEqual(set(captions), required)
                for value in captions.values():
                    self.assertIsInstance(value, str)
                    self.assertTrue(value.strip())
                if name.startswith("open-ai"):
                    for key in required - {"priority"}:
                        self.assertIn("{0}", captions[key])
                        captions[key].format(1)

    def test_packaged_help_images_and_default_frame_exist(self):
        help_folder = RESOURCES / "Help"
        self.assertEqual({path.name for path in help_folder.glob("*.jpg")},
                         {"1-1.jpg", "1-2.jpg", "1-3.jpg", "2.jpg", "3.jpg", "4.jpg",
                          "5-1.jpg", "5-2.jpg"})
        for path in help_folder.glob("*.jpg"):
            with self.subTest(file=path.name):
                image = path.read_bytes()
                self.assertTrue(image.startswith(b"\xff\xd8") and image.endswith(b"\xff\xd9"))
        defaults = load_json(RESOURCES / "Json" / "default-values.json")
        self.assertTrue((RESOURCES / defaults["FrameImagePath"]).is_file())


if __name__ == "__main__":
    unittest.main()
