"""Fine-tune usas-slm trên máy có GPU NVIDIA (Windows cmd), cùng thông số với train_slm.ipynb. Đọc HUONG_DAN_TRAIN_LOCAL.md.

File này chạy độc lập, không cần mã của dự án: chỉ cần train.jsonl và val.jsonl.
  python train_slm.py --check                 chỉ kiểm 2 file dữ liệu (không cần GPU)
  python train_slm.py --version v7 --bench 40 chạy thử 40 bước: đo tốc độ, bộ nhớ, ước lượng giờ
  python train_slm.py --version v7            train thật; bị ngắt thì chạy lại đúng lệnh này để tiếp tục
  python train_slm.py --version v7 --merge    gộp LoRA đã train thành model 16-bit để đổi sang GGUF
"""

import argparse
import csv
import glob
import inspect
import json
import os
import sys

MODEL_NAME = "unsloth/Qwen3-4B-Instruct-2507"
LR = 2e-4
LORA_R = 16
EVAL_EVERY = 250
VAL_MAX = 300
PATIENCE = 2


def parse_args():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--version", default="v7", help="tên lần train; dữ liệu mới thì đổi tên")
    p.add_argument("--seed", type=int, default=3407)
    p.add_argument("--data", default="data", help="thư mục chứa train.jsonl và val.jsonl")
    p.add_argument("--out", default="ket_qua", help="thư mục lưu kết quả")
    p.add_argument("--max-seq", type=int, default=3072)
    p.add_argument("--epochs", type=int, default=2)
    p.add_argument("--batch", type=int, default=4)
    p.add_argument("--grad-accum", type=int, default=4, help="batch x grad-accum luôn nên bằng 16")
    p.add_argument("--bench", type=int, default=0, help="số bước chạy thử, không lưu gì")
    p.add_argument("--check", action="store_true", help="chỉ kiểm dữ liệu")
    p.add_argument("--merge", action="store_true", help="gộp LoRA đã lưu thành model 16-bit")
    return p.parse_args()


def check_data(folder):
    prompts = set()
    for name in ("train.jsonl", "val.jsonl"):
        path = os.path.join(folder, name)
        if not os.path.exists(path):
            sys.exit(f"Thiếu file {path}")
        count, longest = 0, 0
        with open(path, encoding="utf-8") as f:
            for line in f:
                if not line.strip():
                    continue
                messages = json.loads(line)["messages"]
                assert [m["role"] for m in messages] == ["system", "user", "assistant"], f"{name}: sai dạng hội thoại"
                assert "intents" in json.loads(messages[2]["content"]), f"{name}: đáp án không có khóa intents (file cũ?)"
                prompts.add(messages[0]["content"])
                longest = max(longest, sum(len(m["content"]) for m in messages))
                count += 1
        print(f"{name}: {count} hội thoại, dài nhất {longest} ký tự")
    assert len(prompts) == 1, "train và val phải dùng chung đúng một prompt"
    print("Prompt:", next(iter(prompts)).splitlines()[0])


def accepted(cls, wanted, alternatives=()):
    """Tên tham số của TRL / transformers đổi giữa các phiên bản: chỉ truyền tên mà bản đang cài nhận.
    wanted: {tên: giá trị}. alternatives: {tên: (tên mới, giá trị mới)}. Không có cách nào thì dừng, không bỏ qua lặng lẽ."""
    known = inspect.signature(cls.__init__).parameters
    out = {}
    for key, value in wanted.items():
        if key in known:
            out[key] = value
            continue
        new = dict(alternatives).get(key)
        if new and new[0] in known:
            print(f"[tương thích] {key} -> {new[0]}={new[1]!r}")
            out[new[0]] = new[1]
        else:
            sys.exit(f"Bản thư viện đang cài không nhận tham số '{key}' của {cls.__name__}. Gửi nguyên dòng này cho người giao việc.")
    return out


def merge(args, out_dir):
    from unsloth import FastLanguageModel

    lora = os.path.join(out_dir, "lora")
    assert os.path.isdir(lora), f"Chưa có {lora}: train xong đã rồi mới gộp"
    model, tokenizer = FastLanguageModel.from_pretrained(model_name=lora, max_seq_length=args.max_seq, load_in_4bit=True, dtype=None)
    target = os.path.join(out_dir, "merged")
    model.save_pretrained_merged(target, tokenizer, save_method="merged_16bit")
    print("Đã gộp xong:", target)


def main():
    args = parse_args()
    if sys.stdout.encoding.lower() != "utf-8":
        sys.stdout.reconfigure(encoding="utf-8")
    tag = args.version if args.seed == 3407 else f"{args.version}_seed{args.seed}"
    out_dir = os.path.abspath(os.path.join(args.out, tag))
    if args.merge:
        merge(args, out_dir)
        return
    check_data(args.data)
    if args.check:
        return

    # unsloth phải được import trước transformers và trl
    from unsloth import FastLanguageModel  # noqa: I001
    from unsloth.chat_templates import get_chat_template, train_on_responses_only

    import torch
    from datasets import load_dataset
    from transformers import EarlyStoppingCallback
    from transformers.trainer_callback import PrinterCallback
    from trl import SFTConfig, SFTTrainer

    assert torch.cuda.is_available(), "PyTorch không thấy GPU: cài lại bản PyTorch có CUDA"
    print("GPU:", torch.cuda.get_device_name(0))

    model, tokenizer = FastLanguageModel.from_pretrained(
        model_name=MODEL_NAME, max_seq_length=args.max_seq, load_in_4bit=True, dtype=None
    )
    model = FastLanguageModel.get_peft_model(
        model,
        r=LORA_R,
        target_modules=["q_proj", "k_proj", "v_proj", "o_proj", "gate_proj", "up_proj", "down_proj"],
        lora_alpha=LORA_R,
        lora_dropout=0,
        bias="none",
        use_gradient_checkpointing="unsloth",
        random_state=args.seed,
    )
    tokenizer = get_chat_template(tokenizer, chat_template="qwen3-instruct")
    data = load_dataset(
        "json",
        data_files={"train": os.path.join(args.data, "train.jsonl"), "val": os.path.join(args.data, "val.jsonl")},
    )

    def to_text(batch):
        return {"text": [tokenizer.apply_chat_template(m, tokenize=False, add_generation_prompt=False) for m in batch["messages"]]}

    data = data.map(to_text, batched=True)
    lengths = [len(tokenizer(t)["input_ids"]) for t in data["train"]["text"]]
    lengths += [len(tokenizer(t)["input_ids"]) for t in data["val"]["text"]]
    print(f"Số token: trung bình {sum(lengths) // len(lengths)}, lớn nhất {max(lengths)}, giới hạn {args.max_seq}")
    assert max(lengths) < args.max_seq, "Có hội thoại dài quá --max-seq: đáp án sẽ bị cắt. Tăng --max-seq."
    data["val"] = data["val"].shuffle(seed=args.seed).select(range(min(VAL_MAX, len(data["val"]))))

    bench = args.bench
    config = accepted(
        SFTConfig,
        dict(
            dataset_text_field="text",
            max_seq_length=args.max_seq,
            dataset_num_proc=1,  # Windows: nhiều tiến trình dễ treo hoặc sập
            per_device_train_batch_size=args.batch,
            per_device_eval_batch_size=args.batch,
            gradient_accumulation_steps=args.grad_accum,
            num_train_epochs=args.epochs,
            max_steps=bench if bench else -1,
            group_by_length=True,
            learning_rate=LR,
            warmup_ratio=0.05,
            lr_scheduler_type="linear",
            optim="adamw_8bit",
            weight_decay=0.01,
            eval_strategy="no" if bench else "steps",
            eval_steps=EVAL_EVERY,
            save_strategy="no" if bench else "steps",
            save_steps=EVAL_EVERY,
            save_total_limit=2,
            load_best_model_at_end=not bench,
            metric_for_best_model="eval_loss",
            greater_is_better=False,
            logging_steps=10,
            seed=args.seed,
            report_to="none",
            output_dir=os.path.join(out_dir, "checkpoints"),
        ),
        alternatives={
            "max_seq_length": ("max_length", args.max_seq),
            "group_by_length": ("train_sampling_strategy", "group_by_length"),
            "warmup_ratio": ("warmup_steps", 0.05),
        },
    )
    tokenizer_key = "tokenizer" if "tokenizer" in inspect.signature(SFTTrainer.__init__).parameters else "processing_class"
    trainer = SFTTrainer(
        model=model,
        train_dataset=data["train"],
        eval_dataset=data["val"],
        args=SFTConfig(**config),
        callbacks=[] if bench else [EarlyStoppingCallback(early_stopping_patience=PATIENCE)],
        **{tokenizer_key: tokenizer},
    )
    trainer.remove_callback(PrinterCallback)  # giữ thanh tiến độ, bỏ dòng in loss từng bước (nhật ký vẫn ghi)
    trainer = train_on_responses_only(trainer, instruction_part="<|im_start|>user\n", response_part="<|im_start|>assistant\n")

    # Kiểm phần bị che: chỉ đáp án JSON được tính lỗi. Sai thì dừng trước khi tốn giờ GPU
    labels = trainer.train_dataset[5]["labels"]
    pad = tokenizer.pad_token_id
    shown = tokenizer.decode([pad if x == -100 else x for x in labels]).replace(tokenizer.pad_token, " ")
    assert "PHÂN LOẠI TIN NHẮN" not in shown and '"intents"' in shown, "Che sai: còn prompt hoặc mất đáp án"
    print("Phần được tính lỗi (mẫu):", shown.strip()[:200])

    has_checkpoint = not bench and bool(glob.glob(os.path.join(out_dir, "checkpoints", "checkpoint-*")))
    print("Có checkpoint cũ, TIẾP TỤC từ đó" if has_checkpoint else "Train từ đầu")
    stats = trainer.train(resume_from_checkpoint=has_checkpoint)

    if bench:
        per_step = stats.metrics["train_runtime"] / bench
        total = len(data["train"]) // (args.batch * args.grad_accum) * args.epochs
        gpu_total = torch.cuda.get_device_properties(0).total_memory / 1e9
        print(f"\nTốc độ: {per_step:.1f} giây/bước")
        print(f"Bộ nhớ GPU đỉnh: {torch.cuda.max_memory_allocated() / 1e9:.1f} / {gpu_total:.1f} GB")
        print(f"Ước lượng {total} bước: {total * per_step / 3600:.1f} giờ (chưa tính các lần kiểm val và lưu checkpoint)")
        return

    os.makedirs(out_dir, exist_ok=True)
    log = trainer.state.log_history
    keys = sorted({k for row in log for k in row})
    with open(os.path.join(out_dir, "training_log.csv"), "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=keys)
        writer.writeheader()
        writer.writerows(log)
    with open(os.path.join(out_dir, "trainer_state.json"), "w", encoding="utf-8") as f:
        json.dump(log, f, ensure_ascii=False, indent=1)
    model.save_pretrained(os.path.join(out_dir, "lora"))
    tokenizer.save_pretrained(os.path.join(out_dir, "lora"))
    versions = {}
    for name in ("unsloth", "torch", "transformers", "trl", "peft", "bitsandbytes"):
        versions[name] = getattr(sys.modules.get(name), "__version__", None)
    info = dict(vars(args), model=MODEL_NAME, gpu=torch.cuda.get_device_name(0), versions=versions, best_val_loss=trainer.state.best_metric)
    with open(os.path.join(out_dir, "thong_tin_train.json"), "w", encoding="utf-8") as f:
        json.dump(info, f, ensure_ascii=False, indent=1)
    print("Train xong. val loss tốt nhất:", trainer.state.best_metric, "| kết quả:", out_dir)
    print("Bước tiếp theo: chạy lại với --merge")


if __name__ == "__main__":
    main()
