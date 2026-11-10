"use client";

import { useState } from "react";
import Link from "next/link";

interface SchoolPhotoProps {
    name: string;
    coverUrl?: string | null;
    logoUrl?: string | null;
    href?: string; // có thì bấm vào ảnh sẽ sang trang này (trang chi tiết trường)
}

// Chữ cái đầu của tên trường khi không có ảnh nào ("Occidental College" thành "OC")
function initialsOf(name: string) {
    const words = name.split(/\s+/).filter((word) => /^[A-Za-z]/.test(word) && word[0] === word[0].toUpperCase());
    return (words.length > 1 ? words[0][0] + words[1][0] : name.slice(0, 2)).toUpperCase();
}

// Ảnh bên trái thẻ trường, kiểu CollegeVine: ảnh bìa cao hết thẻ, logo nhỏ ở góc.
// Thiếu ảnh bìa thì hiện logo giữa nền nhạt; thiếu cả hai (hoặc ảnh lỗi) thì hiện chữ cái đầu, để các thẻ vẫn thẳng hàng.
// Ảnh đã là WebP nhỏ trên CDN Cloudinary nên không cần qua bộ tối ưu ảnh của Next.
export function SchoolPhoto({ name, coverUrl, logoUrl, href }: SchoolPhotoProps) {
    const [failed, setFailed] = useState<string[]>([]);
    const cover = coverUrl && !failed.includes(coverUrl) ? coverUrl : null;
    const logo = logoUrl && !failed.includes(logoUrl) ? logoUrl : null;
    const fail = (url: string) => setFailed((cur) => (cur.includes(url) ? cur : [...cur, url]));

    const photo = (
        <div className="relative h-40 w-full shrink-0 overflow-hidden bg-brand-wash @2xl:h-auto @2xl:w-44 @4xl:w-56">
            {cover ? (
                <>
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img
                        src={cover}
                        alt={`Ảnh ${name}`}
                        loading="lazy"
                        className="absolute inset-0 size-full object-cover"
                        onError={() => fail(cover)}
                    />
                    {logo && (
                        <div className="absolute bottom-2 left-2 flex size-12 items-center justify-center rounded-lg bg-white p-1 ">
                            {/* eslint-disable-next-line @next/next/no-img-element */}
                            <img
                                src={logo}
                                alt={`Logo ${name}`}
                                loading="lazy"
                                className="size-full object-contain"
                                onError={() => fail(logo)}
                            />
                        </div>
                    )}
                </>
            ) : logo ? (
                <div className="absolute inset-0 flex items-center justify-center p-6">
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img
                        src={logo}
                        alt={`Logo ${name}`}
                        loading="lazy"
                        className="max-h-24 w-full object-contain"
                        onError={() => fail(logo)}
                    />
                </div>
            ) : (
                <div aria-hidden className="absolute inset-0 flex items-center justify-center text-3xl font-bold text-brand/50">
                    {initialsOf(name)}
                </div>
            )}
        </div>
    );
    // Bấm vào ảnh sang trang chi tiết trường; ảnh vẫn cao hết thẻ nhờ khung flex kéo giãn
    return href ? (
        <Link href={href} aria-label={`Xem thông tin ${name}`} className="flex shrink-0 @2xl:self-stretch">
            {photo}
        </Link>
    ) : (
        photo
    );
}
