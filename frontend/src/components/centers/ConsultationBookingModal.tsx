"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { CheckCircle2Icon, CalendarIcon, SparklesIcon } from "lucide-react";

interface ConsultationBookingModalProps {
  centerName: string;
}

export function ConsultationBookingModal({ centerName }: ConsultationBookingModalProps) {
  const [open, setOpen] = useState(false);
  const [mode, setMode] = useState<"offline" | "online">("offline");
  const [isSubmitted, setIsSubmitted] = useState(false);
  const [loading, setLoading] = useState(false);

  // Form states
  const [fullName, setFullName] = useState("");
  const [phone, setPhone] = useState("");
  const [email, setEmail] = useState("");
  const [targetDegree, setTargetDegree] = useState("undergraduate");
  const [note, setNote] = useState("");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setTimeout(() => {
      setLoading(false);
      setIsSubmitted(true);
    }, 600);
  };

  const handleReset = () => {
    setIsSubmitted(false);
    setOpen(false);
    setFullName("");
    setPhone("");
    setEmail("");
    setNote("");
  };

  return (
    <>
      <Button
        size="lg"
        onClick={() => setOpen(true)}
        className="gap-2 bg-brand text-white hover:bg-brand/90 font-medium "
      >
        <CalendarIcon className="w-4 h-4 text-white" />
        Đặt lịch tư vấn 1-1 miễn phí
      </Button>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-lg">
          {isSubmitted ? (
            <div className="py-6 text-center space-y-4">
              <div className="w-14 h-14 bg-ok-wash text-ok rounded-full flex items-center justify-center mx-auto">
                <CheckCircle2Icon className="w-8 h-8" />
              </div>
              <DialogHeader>
                <DialogTitle className="text-xl text-center">Gửi yêu cầu hẹn thành công!</DialogTitle>
                <DialogDescription className="text-center text-sm pt-2">
                  Hệ thống đã chuyển thông tin của bạn tới ban tư vấn của <strong>{centerName}</strong>. Chuyên viên sẽ gọi điện thoại hoặc gửi email xác nhận lịch trong vòng <strong>2 giờ làm việc</strong>.
                </DialogDescription>
              </DialogHeader>
              <div className="pt-4">
                <Button onClick={handleReset} className="w-full">
                  Hoàn tất
                </Button>
              </div>
            </div>
          ) : (
            <form onSubmit={handleSubmit}>
              <DialogHeader>
                <div className="flex items-center gap-2 text-brand text-xs font-semibold">
                  <SparklesIcon className="w-3.5 h-3.5" />
                  Kết nối trực tiếp trung tâm
                </div>
                <DialogTitle className="text-xl">Đặt lịch hẹn tư vấn 1-1</DialogTitle>
                <DialogDescription className="text-xs">
                  Tư vấn lộ trình, săn học bổng và rà soát hồ sơ cùng chuyên gia tại <strong>{centerName}</strong>.
                </DialogDescription>
              </DialogHeader>

              <div className="grid gap-3.5 py-4">
                <div className="space-y-1.5">
                  <Label htmlFor="c-name" className="text-xs font-medium">
                    Họ và tên học sinh / phụ huynh <span className="text-destructive">*</span>
                  </Label>
                  <Input
                    id="c-name"
                    placeholder="Ví dụ: Nguyễn Văn An"
                    value={fullName}
                    onChange={(e) => setFullName(e.target.value)}
                    required
                  />
                </div>

                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1.5">
                    <Label htmlFor="c-phone" className="text-xs font-medium">
                      Số điện thoại <span className="text-destructive">*</span>
                    </Label>
                    <Input
                      id="c-phone"
                      type="tel"
                      placeholder="0988 123 456"
                      value={phone}
                      onChange={(e) => setPhone(e.target.value)}
                      required
                    />
                  </div>
                  <div className="space-y-1.5">
                    <Label htmlFor="c-email" className="text-xs font-medium">
                      Email
                    </Label>
                    <Input
                      id="c-email"
                      type="email"
                      placeholder="example@gmail.com"
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                    />
                  </div>
                </div>

                <div className="space-y-1.5">
                  <Label className="text-xs font-medium">Hình thức tư vấn</Label>
                  <RadioGroup
                    value={mode}
                    onValueChange={(v) => setMode(v as "offline" | "online")}
                    className="grid grid-cols-2 gap-2"
                  >
                    <div className="flex items-center space-x-2 border rounded-md p-2 hover:bg-muted/50 cursor-pointer">
                      <RadioGroupItem value="offline" id="mode-offline" />
                      <Label htmlFor="mode-offline" className="text-xs cursor-pointer">
                        Trực tiếp tại trung tâm
                      </Label>
                    </div>
                    <div className="flex items-center space-x-2 border rounded-md p-2 hover:bg-muted/50 cursor-pointer">
                      <RadioGroupItem value="online" id="mode-online" />
                      <Label htmlFor="mode-online" className="text-xs cursor-pointer">
                        Trực tuyến qua Meet / Zoom
                      </Label>
                    </div>
                  </RadioGroup>
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="c-degree" className="text-xs font-medium">
                    Bậc học mong muốn
                  </Label>
                  <select
                    id="c-degree"
                    className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                    value={targetDegree}
                    onChange={(e) => setTargetDegree(e.target.value)}
                  >
                    <option value="undergraduate">Đại học 4 năm (Cử nhân)</option>
                    <option value="community_college">Cao đẳng cộng đồng 2+2</option>
                    <option value="master">Thạc sĩ (Master / MBA)</option>
                    <option value="highschool">Trung học phổ thông (Lớp 9-12)</option>
                    <option value="test_prep">Chỉ học luyện thi SAT / IELTS</option>
                  </select>
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="c-note" className="text-xs font-medium">
                    Nhu cầu chi tiết (GPA hiện tại, ngành học, mức ngân sách...)
                  </Label>
                  <Textarea
                    id="c-note"
                    placeholder="Ví dụ: Em muốn nộp ngành CS kỳ Thu 2027, GPA 8.5, cần tư vấn gói sửa bài luận..."
                    rows={2}
                    value={note}
                    onChange={(e) => setNote(e.target.value)}
                  />
                </div>
              </div>

              <DialogFooter>
                <Button type="button" variant="outline" onClick={() => setOpen(false)}>
                  Hủy
                </Button>
                <Button type="submit" disabled={loading || !fullName || !phone}>
                  {loading ? "Đang gửi..." : "Gửi yêu cầu hẹn"}
                </Button>
              </DialogFooter>
            </form>
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}
