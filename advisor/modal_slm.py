import os
from pathlib import Path

import modal

app = modal.App("usas-slm")
volume = modal.Volume.from_name("usas-models", create_if_missing=True)

image = modal.Image.from_registry("nvidia/cuda:12.4.1-runtime-ubuntu22.04", add_python="3.11").pip_install(
    "fastapi",
    "pydantic",
    "llama-cpp-python",
    "huggingface_hub",
    extra_index_url="https://abetlen.github.io/llama-cpp-python/whl/cu124",
)

HF_REPO_ID = os.environ.get("SLM_HF_REPO", "Dunghthe04/usas-slm")
HF_MODEL_FILENAME = os.environ.get("SLM_HF_FILENAME", "usas-slm-v4.Q4_K_M.gguf")
MODEL_NAME = os.environ.get("SLM_MODEL_NAME", "usas-slm-v4")


@app.cls(
    image=image,
    gpu="T4",
    volumes={"/models": volume},
    scaledown_window=120,
)
class ModelServer:
    @modal.enter()
    def load_model(self):
        from huggingface_hub import hf_hub_download
        from llama_cpp import Llama

        local_dir = Path("/models")
        local_dir.mkdir(parents=True, exist_ok=True)
        cached_model = local_dir / HF_MODEL_FILENAME

        try:
            model_path = hf_hub_download(
                repo_id=HF_REPO_ID,
                filename=HF_MODEL_FILENAME,
                local_dir=str(local_dir),
            )
            try:
                volume.commit()
            except Exception:
                pass
        except Exception:
            if cached_model.is_file():
                model_path = str(cached_model)
            else:
                raise

        self.llm = Llama(
            model_path=model_path,
            n_gpu_layers=-1,
            n_ctx=4096,
            verbose=False,
        )

    @modal.asgi_app()
    def serve(self):
        from fastapi import FastAPI, Request

        api = FastAPI(title="USAS SLM Service")

        @api.get("/")
        @api.get("/health")
        def health():
            return {"status": "ok", "model": MODEL_NAME}

        @api.get("/api/tags")
        def tags():
            return {
                "models": [
                    {
                        "name": f"{MODEL_NAME}:latest",
                        "model": MODEL_NAME,
                        "details": {"family": "qwen", "parameter_size": "4B"},
                    }
                ]
            }

        @api.post("/api/chat")
        async def chat(request: Request):
            body = await request.json()
            messages = body.get("messages", [])
            options = body.get("options", {})
            temperature = options.get("temperature", 0.0)
            schema = body.get("format")

            response_format = None
            if schema:
                if isinstance(schema, dict):
                    response_format = {"type": "json_object", "schema": schema}
                elif schema == "json":
                    response_format = {"type": "json_object"}

            response = self.llm.create_chat_completion(
                messages=messages,
                temperature=temperature,
                response_format=response_format,
            )

            choice = response["choices"][0]
            content = choice["message"].get("content", "")

            return {
                "model": MODEL_NAME,
                "message": {
                    "role": "assistant",
                    "content": content,
                },
                "done": True,
            }

        return api


ocr_image = (
    modal.Image.debian_slim(python_version="3.11")
    .apt_install("curl", "zstd")
    .run_commands("curl -fsSL https://ollama.com/install.sh | sh")
    .pip_install("fastapi", "httpx")
)


@app.cls(
    image=ocr_image,
    gpu="T4",
    volumes={"/root/.ollama": volume},
    scaledown_window=120,
)
class OcrServer:
    @modal.enter()
    def start_ollama(self):
        import json
        import subprocess
        import time
        import urllib.request

        self.proc = subprocess.Popen(["ollama", "serve"])
        for _ in range(40):
            try:
                urllib.request.urlopen("http://localhost:11434/api/tags", timeout=1)
                break
            except Exception:
                time.sleep(0.5)

        has_model = False
        try:
            req = urllib.request.urlopen("http://localhost:11434/api/tags")
            tags = json.loads(req.read().decode())
            has_model = any("glm-ocr" in m.get("name", "") for m in tags.get("models", []))
        except Exception:
            pass

        if not has_model:
            subprocess.run(["ollama", "pull", "glm-ocr"], check=True)

    @modal.asgi_app()
    def serve(self):
        import httpx
        from fastapi import FastAPI, Request, Response

        api = FastAPI(title="USAS GLM-OCR Service")

        @api.get("/health")
        def health():
            return {"status": "ok", "service": "glm-ocr"}

        @api.api_route("/{path:path}", methods=["GET", "POST", "PUT", "DELETE"])
        async def proxy_to_ollama(request: Request, path: str):
            async with httpx.AsyncClient(base_url="http://localhost:11434", timeout=300.0) as client:
                body = await request.body()
                resp = await client.request(
                    method=request.method,
                    url=f"/{path}",
                    params=request.query_params,
                    headers={k: v for k, v in request.headers.items() if k.lower() != "host"},
                    content=body,
                )
                return Response(
                    content=resp.content,
                    status_code=resp.status_code,
                    headers={
                        k: v
                        for k, v in resp.headers.items()
                        if k.lower() not in ("content-length", "content-encoding")
                    },
                )

        return api
