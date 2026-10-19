# NetMvcAuthV2

Modul autentikasi ASP.NET Core MVC (.NET 10): register, login, logout, lupa/reset password, role `User` / `Admin`.
Spesifikasi lengkap ada di [`prd.md`](prd.md).

## Menjalankan

```bash
# 1. Sesuaikan ConnectionStrings:DefaultConnection di appsettings.json bila perlu
# 2. Buat database
dotnet ef database update

# 3. Jalankan
dotnet run --launch-profile https
```

Login memakai cookie `Secure`, jadi buka lewat **https**.

## Akun Admin (seed)

Admin tidak bisa dibuat lewat form register. Di environment **Development**, akun admin dibuat otomatis saat startup
(idempotent: kalau emailnya sudah ada, tidak dibuat lagi). Kredensialnya diambil dari user-secrets, bukan dari file yang di-commit:

```bash
dotnet user-secrets set "Seed:AdminEmail" "admin@example.com"
dotnet user-secrets set "Seed:AdminPassword" "GantiPasswordIni1"
dotnet user-secrets set "Seed:AdminName" "Administrator"   # opsional
```

Kalau `Seed:AdminEmail` atau `Seed:AdminPassword` kosong, seeder dilewati.

## Email (SMTP)

- **Development**: `FakeEmailSender` tidak mengirim apa-apa. Link reset password ditulis ke log/console.
- **Selain Development**: `SmtpEmailSender` (MailKit). Host/port/pengirim ada di section `Smtp` pada `appsettings.json`;
  kredensial **jangan** ditaruh di sana:

```bash
dotnet user-secrets set "Smtp:Host" "smtp.example.com"
dotnet user-secrets set "Smtp:User" "username"
dotnet user-secrets set "Smtp:Password" "password"
```

Di production, pakai environment variable: `Smtp__Host`, `Smtp__User`, `Smtp__Password`.

## Sebelum deploy

- Isi `AllowedHosts` di `appsettings.json` dengan domain asli. Link reset password dibangun dari host request,
  jadi `*` membuka celah host header injection.
- Kalau di belakang reverse proxy, aktifkan `UseForwardedHeaders` supaya rate limiter membaca IP klien yang benar.

## Test

```bash
dotnet test tests/NetMvcAuthV2.Tests
```

Mencakup `AuthService` (register, login + lockout, forgot, reset) dan `UserMappings`.
