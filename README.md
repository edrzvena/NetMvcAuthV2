# NetMvcAuthV2

Modul autentikasi **ASP.NET Core MVC (.NET 10)**: register, login, logout, lupa/reset password, dan pembagian role `User` / `Admin`.

## Daftar Isi

1. [Tujuan Project](#1-tujuan-project)
2. [Fitur](#2-fitur)
3. [Teknologi dan Versi](#3-teknologi-dan-versi)
4. [Library yang Dipakai dan Cara Install](#4-library-yang-dipakai-dan-cara-install)
5. [Instalasi dari Nol](#5-instalasi-dari-nol)
6. [Menjalankan Aplikasi](#6-menjalankan-aplikasi)
7. [Akun Admin (Seed)](#7-akun-admin-seed)
8. [Email (SMTP)](#8-email-smtp)
9. [Struktur Project Singkat](#9-struktur-project-singkat)
10. [Test](#10-test)
11. [Sebelum Deploy](#11-sebelum-deploy)
12. [Dokumentasi Lain](#12-dokumentasi-lain)

---

## 1. Tujuan Project

Hampir semua aplikasi perlu tahu **siapa yang sedang mengakses** dan **apa yang boleh dilakukannya**. Project ini menyediakan fondasi
autentikasi dan otorisasi yang aman, rapi, dan bisa dipakai ulang, sehingga fitur lain cukup memasang `[Authorize]` atau
`[Authorize(Roles = Roles.Admin)]` tanpa membuat sistem login dari awal.

Tujuan konkretnya:

- Pengunjung dapat **mendaftar**, **masuk** (opsi "Ingat saya"), dan **keluar**.
- Pengguna yang lupa password dapat **memulihkannya sendiri** lewat link yang dikirim ke email.
- Ada pembagian **role** (`User` dan `Admin`) yang dilindungi di sisi server, bukan hanya disembunyikan di tampilan.
- Password dan sesi login ditangani dengan praktik keamanan yang benar (hash, lockout, rate limit, token sekali pakai).
- Kode **terstruktur berlapis** (Controller → Service → Repository → DbContext, dengan DTO, interface, dan mapping) agar mudah dibaca,
  diuji, dan dikembangkan.

Modul ini **sengaja dibuat custom ringan** (cookie authentication + `PasswordHasher<T>`), bukan ASP.NET Core Identity penuh, agar
konsepnya jelas dan mudah dijelaskan. Yang di luar scope: login sosial (Google/Facebook), 2FA, dan manajemen user oleh admin.
Spesifikasi lengkap ada di [`prd.md`](prd.md).

---

## 2. Fitur

| Fitur | Keterangan |
|---|---|
| Register | Email + nama + password, email unik, role otomatis `User` |
| Login + "Ingat saya" | Cookie sesi, atau cookie persisten 14 hari |
| Logout | Hanya lewat POST + anti-forgery token |
| Lupa password | Link reset dikirim ke email; respons sama baik email terdaftar maupun tidak |
| Reset password | Token acak sekali pakai, kedaluwarsa 1 jam, sesi lama otomatis tidak berlaku |
| Access Denied | Halaman 403 untuk user yang rolenya tidak cukup |
| Proteksi brute-force | Akun dikunci 10 menit setelah 5x gagal + rate limit per IP |
| Seed Admin | Akun admin dibuat otomatis di Development |
| Beranda | Landing page untuk tamu, dashboard untuk user yang sudah login |
| Panel Admin | Halaman contoh khusus role `Admin` |

---

## 3. Teknologi dan Versi

| Komponen | Versi / Pilihan |
|---|---|
| **Bahasa** | C# (nullable reference types dan implicit usings aktif) |
| **Framework** | **.NET 10** (`net10.0`), ASP.NET Core MVC |
| **.NET SDK yang dipakai** | 10.0.x (dikembangkan dengan SDK 10.0.301, runtime ASP.NET Core 10.0.9) |
| **ORM** | Entity Framework Core **10.0.x** |
| **Database** | **Microsoft SQL Server** (dikembangkan dengan SQL Server Express, instance `.\SQLEXPRESS`) |
| **Template view** | Razor (`.cshtml`) |
| **Autentikasi** | Cookie authentication bawaan ASP.NET Core |
| **CSS framework** | **Bootstrap 5.3.3** |
| **CSS kustom** | `wwwroot/css/site.css` (CSS biasa, pakai CSS variable) + *scoped CSS* Razor (`*.cshtml.css`) |
| **JavaScript** | JavaScript biasa (`wwwroot/js/auth.js`) + jQuery 3.7.1 |
| **Validasi klien** | jQuery Validation 1.21.0 + jQuery Validation Unobtrusive 4.0.0 |
| **Ikon** | Karakter Unicode/emoji dan SVG (logo); tanpa library ikon |
| **Font** | Font sistem bawaan Bootstrap; tanpa font eksternal |
| **Build tool frontend** | **Tidak ada** (tanpa npm, Sass, Tailwind, atau bundler); semua file statis dipakai langsung |
| **Test** | xUnit 2.9.3 + NSubstitute 6.2.0 |

Tidak perlu `npm install`: pustaka frontend sudah disertakan di `wwwroot/lib/`.

---

## 4. Library yang Dipakai dan Cara Install

### 4.1 Paket NuGet (aplikasi utama)

Semua paket sudah tercantum di `NetMvcAuthV2.csproj`, jadi cukup menjalankan `dotnet restore` (otomatis saat `dotnet build` / `dotnet run`).
Perintah di bawah hanya perlu bila Anda memasangnya manual di project baru.

| Paket | Versi | Fungsi di project ini | Perintah install |
|---|---|---|---|
| `Microsoft.EntityFrameworkCore.SqlServer` | 10.0.x | ORM + driver SQL Server (`AppDbContext`, query repository) | `dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version "10.0.*"` |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.x | Dibutuhkan perintah `dotnet ef` untuk membuat migration (hanya saat development) | `dotnet add package Microsoft.EntityFrameworkCore.Design --version "10.0.*"` |
| `MailKit` | 4.x | Kirim email lewat SMTP (`SmtpEmailSender`); membawa `MimeKit` untuk menyusun email | `dotnet add package MailKit --version "4.*"` |

Yang **sudah termasuk di framework** (tidak perlu dipasang): `PasswordHasher<T>` (hash password PBKDF2), cookie authentication,
rate limiter (`AddRateLimiter`), anti-forgery, dan `ILogger`.

### 4.2 Paket NuGet (project test)

Ada di `tests/NetMvcAuthV2.Tests/NetMvcAuthV2.Tests.csproj`.

| Paket | Versi | Fungsi | Perintah install |
|---|---|---|---|
| `xunit` | 2.9.3 | Framework unit test | `dotnet add package xunit --version 2.9.3` |
| `xunit.runner.visualstudio` | 3.1.4 | Agar test terbaca oleh `dotnet test` dan Test Explorer | `dotnet add package xunit.runner.visualstudio --version 3.1.4` |
| `Microsoft.NET.Test.Sdk` | 17.14.1 | Host yang menjalankan test | `dotnet add package Microsoft.NET.Test.Sdk --version 17.14.1` |
| `NSubstitute` | 6.2.0 | Membuat objek tiruan (mock) `IUserRepository`, `IEmailSender`, dll. | `dotnet add package NSubstitute --version 6.2.0` |
| `coverlet.collector` | 6.0.4 | Pengukur code coverage (opsional) | `dotnet add package coverlet.collector --version 6.0.4` |

### 4.3 Tool .NET global

| Tool | Fungsi | Install |
|---|---|---|
| `dotnet-ef` | Membuat migration dan memperbarui database | `dotnet tool install --global dotnet-ef --version "10.*"` |

Sudah terpasang dan ingin memperbarui: `dotnet tool update --global dotnet-ef`. Cek dengan `dotnet ef --version`.

### 4.4 Pustaka frontend (sudah ada di `wwwroot/lib/`)

| Pustaka | Versi | Fungsi |
|---|---|---|
| Bootstrap | 5.3.3 | Grid, komponen (navbar, dropdown, kartu, form, alert), utilitas CSS |
| jQuery | 3.7.1 | Dasar untuk jQuery Validation dan skrip kecil |
| jQuery Validation | 1.21.0 | Validasi form di sisi klien |
| jQuery Validation Unobtrusive | 4.0.0 | Menghubungkan Data Annotations C# ke jQuery Validation lewat atribut `data-val-*` |

File-file ini disalin langsung (tidak lewat npm). Untuk memperbarui, unduh versi baru dan ganti isi folder `wwwroot/lib/<nama>/dist/`
(bisa memakai `libman` bila ingin dikelola otomatis).

> Saat build mungkin muncul peringatan NuGet `NU1902/NU1903` soal `System.Security.Cryptography.Pkcs 9.0.11`. Itu dependensi
> bawaan paket EF Core/SqlClient, bukan kode project ini. Peringatan akan hilang saat paket EF Core diperbarui ke versi yang
> sudah memperbaikinya (`dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version "10.*"`).

---

## 5. Instalasi dari Nol

### 5.1 Prasyarat

| Kebutuhan | Cara mendapatkan |
|---|---|
| **.NET 10 SDK** | Unduh dari <https://dotnet.microsoft.com/download/dotnet/10.0>, atau di Windows: `winget install Microsoft.DotNet.SDK.10`. Cek: `dotnet --version` (harus `10.x`). |
| **SQL Server** (Express/Developer/LocalDB) | Unduh SQL Server Express dari <https://www.microsoft.com/sql-server/sql-server-downloads>. Alternatif ringan: LocalDB (ikut Visual Studio). |
| **dotnet-ef** | `dotnet tool install --global dotnet-ef --version "10.*"` |
| **Git** | <https://git-scm.com/downloads> |
| IDE (opsional) | Visual Studio 2022/2026, VS Code (+ C# Dev Kit), atau JetBrains Rider |

### 5.2 Langkah

```bash
# 1. Ambil kode
git clone <url-repo> NetMvcAuthV2
cd NetMvcAuthV2

# 2. Unduh semua paket NuGet (otomatis dari .csproj)
dotnet restore

# 3. Sesuaikan connection string bila SQL Server Anda berbeda (lihat 5.3)

# 4. Buat database dan tabel
dotnet ef database update

# 5. (Opsional) Siapkan akun admin dan SMTP lewat user-secrets (lihat bagian 7 dan 8)

# 6. Jalankan
dotnet run --launch-profile https
```

### 5.3 Connection string

Atur di `appsettings.json` (`ConnectionStrings:DefaultConnection`). Nilai bawaan project ini:

```json
"DefaultConnection": "Server=.\\SQLEXPRESS;Database=db_net_mvc_auth_v2;Trusted_Connection=True;TrustServerCertificate=True;"
```

Contoh lain:

| Skenario | Connection string |
|---|---|
| SQL Server Express (bawaan) | `Server=.\\SQLEXPRESS;Database=db_net_mvc_auth_v2;Trusted_Connection=True;TrustServerCertificate=True;` |
| LocalDB | `Server=(localdb)\\MSSQLLocalDB;Database=db_net_mvc_auth_v2;Trusted_Connection=True;TrustServerCertificate=True` |
| SQL Server dengan user/password | `Server=localhost;Database=db_net_mvc_auth_v2;User Id=sa;Password=<password>;TrustServerCertificate=True` |

Jangan menaruh password database asli di `appsettings.json` yang di-commit; pakai user-secrets
(`dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."`) atau environment variable.

---

## 6. Menjalankan Aplikasi

```bash
dotnet run --launch-profile https
```

| Profil | URL |
|---|---|
| `https` (disarankan) | <https://localhost:7038> dan <http://localhost:5050> |
| `http` | <http://localhost:5050> |

Login memakai cookie `Secure`, jadi **buka lewat https**. Jika browser menolak sertifikat development, jalankan sekali:

```bash
dotnet dev-certs https --trust
```

Perintah berguna lainnya:

```bash
dotnet build                              # compile
dotnet watch run --launch-profile https   # jalankan dengan hot reload
dotnet ef migrations list                 # lihat migration dan statusnya
```

> Setelah mengubah view atau CSS, hentikan lalu jalankan ulang aplikasi bila tidak memakai `dotnet watch`.
> View dikompilasi saat build; build gagal bila file `.exe` masih dipakai aplikasi yang sedang berjalan.

---

## 7. Akun Admin (Seed)

Admin tidak bisa dibuat lewat form register. Di environment **Development**, akun admin dibuat otomatis **saat aplikasi dijalankan**
(idempotent: kalau emailnya sudah ada, tidak dibuat lagi). Kredensialnya diambil dari user-secrets, bukan dari file yang di-commit:

```bash
dotnet user-secrets set "Seed:AdminEmail" "admin@example.com"
dotnet user-secrets set "Seed:AdminPassword" "GantiPasswordIni1"
dotnet user-secrets set "Seed:AdminName" "Administrator"   # opsional
```

Ketentuan:

- Kalau `Seed:AdminEmail` atau `Seed:AdminPassword` kosong, seeder dilewati.
- Seeder hanya berjalan **saat startup**: setelah mengatur secrets, **jalankan ulang** aplikasi.
- Mengubah `Seed:AdminPassword` setelah admin dibuat **tidak** mengubah password yang tersimpan.
- Password ikut aturan umum: minimal 8 karakter, mengandung huruf dan angka.

---

## 8. Email (SMTP)

- **Development**: `FakeEmailSender` tidak mengirim apa-apa. Link reset password ditulis ke log/console tempat aplikasi berjalan.
- **Selain Development**: `SmtpEmailSender` (MailKit). Host, port, dan pengirim ada di section `Smtp` pada `appsettings.json`;
  kredensial **jangan** ditaruh di sana:

```bash
dotnet user-secrets set "Smtp:Host" "smtp.example.com"
dotnet user-secrets set "Smtp:User" "username"
dotnet user-secrets set "Smtp:Password" "password"
```

Di production, pakai environment variable: `Smtp__Host`, `Smtp__User`, `Smtp__Password`.

---

## 9. Struktur Project Singkat

```text
NetMvcAuthV2/
├── Program.cs               pendaftaran service & pipeline middleware
├── Controllers/             Account, Admin, Home
├── Services/                AuthService, email sender (Smtp/Fake) + interface
├── Repositories/            Repository<T>, UserRepository + interface
├── Data/                    AppDbContext, UserConfiguration, DbSeeder
├── Models/                  User
├── DTOs/Auth/               Register/Login/ForgotPassword/ResetPassword
├── Mappings/                UserMappings (DTO → User, User → claims)
├── Constants/ Common/       konstanta, enum, setting, validator cookie
├── Migrations/              riwayat skema database
├── Views/                   Razor: Account, Admin, Home, Shared
├── wwwroot/                 css, js, img, lib (Bootstrap, jQuery)
└── tests/NetMvcAuthV2.Tests unit test
```

Aturan dependensi satu arah: `Controller → Service → Repository → DbContext`. Controller tidak boleh memakai repository/`Data` langsung.
Penjelasan rinci tiap folder dan file ada di [`FLOW.md`](FLOW.md).

---

## 10. Test

```bash
dotnet test tests/NetMvcAuthV2.Tests
```

Mencakup `AuthService` (register, login + lockout, lupa password, reset password) dan `UserMappings`. Test memakai mock, jadi
**tidak butuh database**.

---

## 11. Sebelum Deploy

- Isi `AllowedHosts` di `appsettings.json` dengan domain asli. Link reset password dibangun dari host request, jadi `*` membuka
  celah *host header injection*.
- Kalau di belakang reverse proxy, aktifkan `UseForwardedHeaders` supaya rate limiter membaca IP klien yang benar.
- Pastikan kredensial SMTP dan connection string production lewat environment variable atau secret manager, bukan file.
- Jalankan `dotnet ef database update` pada database production (seeder admin hanya berjalan di Development).
- Gunakan https; cookie auth ditandai `Secure`.

---

## 12. Dokumentasi Lain

| File | Isi |
|---|---|
| [`prd.md`](prd.md) | Spesifikasi produk: tujuan, acceptance criteria, keamanan, testing |
| [`FLOW.md`](FLOW.md) | Alur lengkap: folder ke folder, file ke file, penjelasan `Program.cs`, dan alur tiap fitur |
