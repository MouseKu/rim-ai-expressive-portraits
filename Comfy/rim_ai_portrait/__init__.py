"""ComfyUI bridge for Rim AI Expressive Portraits' local Images API."""

from .nodes import NODE_CLASS_MAPPINGS, NODE_DISPLAY_NAME_MAPPINGS
from .routes import register_routes

WEB_DIRECTORY = "./web"

register_routes()

