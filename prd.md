# PRD — Modul Autentikasi (Register, Login, Forgot/Reset Password)

> Stack: ASP.NET Core MVC (.NET 10), Entity Framework Core + SQL Server, Razor + Bootstrap.
> Arsitektur berlapis: Controller → Service → Repository → DbContext, dengan DTO, interface, dan mapping.
> Ganti `{App}` di seluruh dokumen dengan nama project (namespace jadi `{App}.DTOs.Auth`, `{App}.Services.Interfaces`, dst).

---

## 1. Latar Belakang & Masalah

Hampir semua fitur aplikasi perlu tahu "siapa yang sedang akses" dan "boleh melakukan apa". Tanpa modul auth yang rapi:

- Data tidak punya pemilik (transaksi, pesanan, ulasan tidak bisa dikaitkan ke akun).
- Halaman khusus admin hanya "disembunyikan" di UI, bukan benar-benar dilindungi.
- Password rawan bocor kalau ditangani asal-asalan.
- User yang lupa password tidak punya jalur pemulihan mandiri, sehingga membebani support.

Modul ini menyediakan fondasi auth yang aman, konsisten, dan mengikuti arsitektur berlapis, sehingga fitur lain cukup memasang `[Authorize]`.

---

## 2. Tujuan & Non-Tujuan

### Tujuan
1. User bisa **mendaftar** dengan email + password.
2. User bisa **login** (opsi "Ingat saya") dan **logout**.
3. User yang lupa password bisa **meminta link reset lewat email** dan **membuat password baru**.
4. Mendukung **role** (`User` / `Admin`) sehingga `[Authorize(Roles = ...)]` langsung bisa dipakai.
5. Kode terstruktur rapi: DTO, interface, repository, service, mapping, constants, view.

### Non-Tujuan (di luar scope)
- Login sosial (Google/Facebook), SSO, OAuth provider.
- 2FA / MFA.
- Manajemen user oleh admin (CRUD user, ubah role). Admin hanya dari seeder.
- Edit profil dan ganti password saat sudah login (backlog v2, lihat §12).
- ASP.NET Core Identity penuh. Modul ini sengaja custom ringan (cookie auth + `PasswordHasher<T>`) agar konsepnya jelas dan mudah dijelaskan.

---

## 3. Target Pengguna & Role

| Role | Nilai enum | Deskripsi |
|---|---|---|
| **User** | `UserRole.User` (default) | Pengguna umum; otomatis jadi role ini saat register. |
| **Admin** | `UserRole.Admin` | Pengelola. **Tidak bisa** dibuat lewat form register; hanya dari seeder / langsung di DB. |

Aturan: register publik **selalu** menghasilkan role default. Role dari form tidak pernah dipercaya (mencegah privilege escalation / over-posting). Nama role bisa disesuaikan dengan domain aplikasi.

---

## 4. Ruang Lingkup Fitur

| # | Fitur | Prioritas |
|---|---|---|
| F1 | Register | MVP |
| F2 | Login + Remember Me | MVP |
| F3 | Logout | MVP |
| F4 | Forgot Password (minta link reset) | MVP |
| F5 | Reset Password (pakai token) | MVP |
| F6 | Halaman Access Denied | MVP |
| F7 | Pengiriman email reset via SMTP (`IEmailSender`) | MVP |
| F8 | Proteksi brute-force (lockout + rate limit) | MVP |
| F9 | Seed akun Admin (Development) | MVP |
| F10 | Ganti password saat login, edit profil | v2 |
| F11 | Verifikasi email saat register | v2 |
| F12 | 2FA | v2 |

---

## 5. User Stories & Acceptance Criteria

### 5.1 Register (F1)
**Sebagai** pengunjung, **saya ingin** membuat akun dengan email & password **agar** bisa memakai fitur yang butuh login.

- Form: Email, Nama Lengkap, Password, Ulangi Password.
- Email wajib, format valid, maks 256 karakter; disimpan ter-normalisasi (`Trim().ToLowerInvariant()`).
- Nama lengkap wajib, maks 100 karakter, di-trim.
- Password minimal 8 karakter, maks 128, mengandung huruf dan angka.
- Ulangi Password harus sama; pesan error: "Password gak sama."
- Email sudah terdaftar → error di form: "Email sudah terdaftar."
- Berhasil → user tersimpan dengan role default, password ter-hash (bukan plain text), redirect ke Login dengan flash sukses: "Registrasi berhasil, silakan login."
- Validasi gagal → form tampil ulang dengan input tetap terisi (kecuali password) dan pesan error per field.
- Keunikan email dijamin juga di level database (unique index), bukan hanya pengecekan di service.

### 5.2 Login (F2)
**Sebagai** user terdaftar, **saya ingin** masuk dengan email & password **agar** bisa mengakses fitur pribadi.

- Form: Email, Password, checkbox "Ingat saya", link "Lupa password?", link "Daftar".
- Email/password salah → pesan **generik yang sama**: "Email atau password salah." (tidak membocorkan email mana yang terdaftar).
- Berhasil → terbit **cookie auth** berisi claim `NameIdentifier` (Id), `Email`, `Name` (FullName), `Role`.
- "Ingat saya" dicentang → cookie persisten (14 hari, sliding); tidak dicentang → session cookie.
- `returnUrl` hanya diikuti jika lokal (`Url.IsLocalUrl`) untuk mencegah open redirect; selain itu ke Home.
- User yang sudah login membuka `/Account/Login` atau `/Account/Register` → redirect ke Home.
- Gagal login berulang → lockout sementara (lihat §5.7).

### 5.3 Logout (F3)
- Hanya lewat **POST** + anti-forgery token (bukan link GET); tombol ada di navbar.
- Menghapus cookie auth, redirect ke Home.

### 5.4 Forgot Password (F4)
**Sebagai** user yang lupa password, **saya ingin** meminta link reset **agar** bisa masuk lagi.

- Form: Email saja.
- Response **selalu sama** baik email terdaftar maupun tidak: "Kalau email itu terdaftar, link reset sudah kami kirim." (tidak ada user enumeration).
- Jika email terdaftar: buat token acak kriptografis (32 byte), simpan **hash SHA-256**-nya + waktu kedaluwarsa (1 jam) di tabel user.
- Link: `https://{host}/Account/ResetPassword?token={tokenMentah}`, dikirim via `IEmailSender`.
- Permintaan ulang → token lama otomatis tidak berlaku (diganti).
- Rate limit: maks 3 permintaan / 15 menit per IP (dan per email).
- Di environment Development, `FakeEmailSender` hanya menulis link ke log/console. Token tidak pernah ditampilkan di halaman.

### 5.5 Reset Password (F5)
- Buka link → form: Token (hidden), Password Baru, Ulangi Password Baru.
- Aturan password sama dengan register.
- Token tidak ada / salah / kedaluwarsa → satu pesan generik: "Token reset password tidak valid atau sudah kedaluwarsa."
- Berhasil → password di-hash ulang, token & expiry di-null-kan (**sekali pakai**), `SecurityStamp` diganti sehingga semua sesi login lama invalid, redirect ke Login dengan flash: "Password berhasil diubah, silakan login."

### 5.6 Access Denied (F6)
- User login tapi role tidak cukup → `/Account/AccessDenied` (halaman 403 ramah, tombol ke Home).
- User belum login membuka halaman `[Authorize]` → redirect ke `/Account/Login?returnUrl=...`.

### 5.7 Proteksi Brute Force (F8)
- Kolom `FailedLoginCount` dan `LockoutEndAt` di `User`.
- 5x gagal berturut-turut → akun dikunci 10 menit. Pesan: "Terlalu banyak percobaan. Coba lagi beberapa menit lagi."
- Login sukses → `FailedLoginCount` kembali 0.
- Tambahan: `AddRateLimiter` (fixed window) pada POST Login / Register / ForgotPassword per IP.

---

## 6. Alur Utama

```
Pengunjung
   │
   ├── Register ──► Validasi ──► Cek email unik ──► Hash password ──► Simpan ──► Redirect Login (flash sukses)
   │
   ├── Login ─────► Validasi ──► Cek lockout ──► Verifikasi hash ──► SignIn cookie ──► returnUrl lokal / Home
   │                                   └─ gagal: ++FailedLoginCount (lockout bila ≥ 5)
   │
   └── Lupa password
          │
          ▼
     ForgotPassword (email) ──► [selalu] halaman konfirmasi generik
          │ (jika email ada)
          ▼
     Buat token, simpan HASH & expiry ──► IEmailSender kirim link
          │
          ▼
     User klik link ──► ResetPassword (token) ──► Validasi token & expiry
          │                                          └─ gagal: error generik
          ▼
     Hash password baru ──► null-kan token ──► ganti SecurityStamp ──► Redirect Login (flash sukses)
```

---

## 7. Arsitektur & Struktur Folder

Aturan dependensi satu arah:

```
Controller  →  Service (interface)  →  Repository (interface)  →  DbContext / Model
     │              │
     └── DTO ───────┘   (Mapping: Model ⇄ DTO / ViewModel)
```

Controller **tidak** boleh `using {App}.Repositories.*` atau `{App}.Data`. Service tidak tahu soal View. Repository tidak berisi aturan bisnis.

```
{App}/
├── Common/
│   └── Enums/UserRole.cs
├── Constants/
│   ├── Roles.cs               const string Admin / User → [Authorize(Roles = Roles.Admin)]
│   ├── AuthSettings.cs        MinPasswordLength, MaxFailedAttempts, LockoutMinutes, ResetTokenLifetime
│   └── CookieNames.cs
├── Controllers/
│   └── AccountController.cs
├── DTOs/Auth/
│   ├── RegisterDto.cs
│   ├── LoginDto.cs
│   ├── ForgotPasswordDto.cs
│   └── ResetPasswordDto.cs
├── Data/
│   ├── AppDbContext.cs
│   ├── Configurations/UserConfiguration.cs   unique index Email, panjang kolom, default Role
│   └── Seed/DbSeeder.cs                      seed akun Admin (Development, idempotent)
├── Mappings/
│   └── UserMappings.cs        RegisterDto → User, User → claims
├── Models/
│   └── User.cs
├── Repositories/
│   ├── Interfaces/{IRepository, IUserRepository}.cs
│   └── Implementations/{Repository, UserRepository}.cs
├── Services/
│   ├── Interfaces/{IAuthService, IEmailSender}.cs
│   └── Implementations/{AuthService, SmtpEmailSender, FakeEmailSender}.cs
├── Views/
│   ├── Account/{Login, Register, ForgotPassword, ForgotPasswordConfirmation, ResetPassword, AccessDenied}.cshtml
│   └── Shared/{_AuthLayout, _ValidationScriptsPartial, _FlashMessages}.cshtml
├── Program.cs
└── tests/{App}.Tests/         AuthServiceTests, UserMappingsTests
```

**DTO vs ViewModel:** DTO = data masuk dari form / dikirim antar lapisan (`RegisterDto`, `LoginDto`). ViewModel = data khusus untuk menampilkan View. Untuk auth, DTO langsung dipakai sebagai `@model` view karena bentuknya 1:1 dengan form.

### Kontrak antar lapisan

| Fitur | Controller action | Service method | Repository method |
|---|---|---|---|
| Register | `Register` GET/POST | `RegisterAsync(RegisterDto)` | `EmailExistsAsync`, `AddAsync`, `SaveChangesAsync` |
| Login | `Login` GET/POST | `LoginAsync(LoginDto)` | `GetByEmailAsync`, `Update`, `SaveChangesAsync` |
| Logout | `Logout` POST | `LogoutAsync()` | — |
| Forgot | `ForgotPassword` GET/POST | `RequestPasswordResetAsync(email, resetLinkBase)` | `GetByEmailAsync`, `Update`, `SaveChangesAsync` |
| Reset | `ResetPassword` GET/POST | `ResetPasswordAsync(ResetPasswordDto)` | `GetByResetTokenHashAsync`, `Update`, `SaveChangesAsync` |

---

## 8. Model Data

### Entity `User` (tabel `Users`)

| Kolom | Tipe | Constraint | Keterangan |
|---|---|---|---|
| `Id` | int | PK, identity | |
| `Email` | nvarchar(256) | **unique index**, required | Disimpan lowercase |
| `FullName` | nvarchar(100) | required | |
| `PasswordHash` | nvarchar(max) | required | Output `PasswordHasher<User>` |
| `Role` | int (enum) | default `User` | |
| `ResetPasswordTokenHash` | nvarchar(64) | nullable, indexed | SHA-256 hex dari token |
| `PasswordResetTokenExpireAt` | datetime2 | nullable | UTC |
| `SecurityStamp` | nvarchar(64) | required | GUID baru tiap ganti password; divalidasi di cookie |
| `FailedLoginCount` | int | default 0 | |
| `LockoutEndAt` | datetime2 | nullable | UTC |
| `CreatedAt` | datetime2 | default UTC now | |

### Migration
- Satu perubahan skema = satu migration, mis. `AddUser`, `AddUserSecurityFields` (`dotnet ef migrations add`).
- Index: `IX_Users_Email` (unique), `IX_Users_ResetPasswordTokenHash`.

### Interface
```csharp
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    Task AddAsync(T entity);
    void Update(T entity);
    void Remove(T entity);
    Task SaveChangesAsync();
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByResetTokenHashAsync(string tokenHash);
    Task<bool> EmailExistsAsync(string email);
}

public record AuthResult(bool Success, string? Error = null);

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterDto dto);
    Task<AuthResult> LoginAsync(LoginDto dto);
    Task LogoutAsync();
    Task RequestPasswordResetAsync(string email, string resetLinkBase);
    Task<AuthResult> ResetPasswordAsync(ResetPasswordDto dto);
}

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody);
}
```

### DTO (Data Annotations, pesan error Bahasa Indonesia)
| DTO | Field |
|---|---|
| `RegisterDto` | `Email` (Required, EmailAddress, MaxLength 256), `FullName` (Required, MaxLength 100, Display "Nama Lengkap"), `Password` (Required, DataType.Password, StringLength 8–128, regex huruf+angka), `ConfirmPassword` (Required, Compare) |
| `LoginDto` | `Email`, `Password`, `RememberMe` (Display "Ingat saya") |
| `ForgotPasswordDto` | `Email` (Required, EmailAddress) |
| `ResetPasswordDto` | `Token` (Required, hidden), `NewPassword`, `ConfirmPassword` |

Field password tidak pernah dikembalikan ke View (kosongkan sebelum `return View(dto)` bila perlu).

---

## 9. Spesifikasi Keamanan (Non-Functional)

| Area | Requirement |
|---|---|
| Hashing password | `PasswordHasher<User>` bawaan (PBKDF2, salt acak). Dilarang MD5/SHA polos. Jika `VerifyHashedPassword` mengembalikan `SuccessRehashNeeded`, hash ulang dan simpan. |
| Token reset | `RandomNumberGenerator.GetBytes(32)` → hex/base64url. Hanya hash SHA-256 yang disimpan. Sekali pakai, kedaluwarsa 1 jam. |
| Cookie | `HttpOnly`, `Secure`, `SameSite=Lax`, sliding expiration 14 hari, nama cookie dari `CookieNames`. |
| CSRF | Semua POST pakai `[ValidateAntiForgeryToken]` (tag helper `asp-` otomatis menyisipkan token). |
| Open redirect | `returnUrl` wajib lolos `Url.IsLocalUrl`. |
| Over-posting | Controller hanya bind ke DTO, bukan ke `User`. Role tidak bisa diisi dari form. |
| User enumeration | Login: pesan generik. Forgot password: response identik. Register: mengungkap "email sudah terdaftar" (trade-off UX yang diterima; solusi lanjutan: verifikasi email di v2). |
| Brute force | Lockout per akun + rate limiter per IP (§5.7). |
| Timing | Login dengan email tak dikenal tetap menjalankan hash dummy agar waktu respons tidak membedakan. |
| Authorization | `[Authorize]` / `[Authorize(Roles = Roles.Admin)]` di server; menyembunyikan tombol di UI tidak cukup. |
| Transport | `UseHttpsRedirection`, `UseHsts` di non-Development. |
| Logging | Serilog: catat login gagal, lockout, reset sukses. **Jangan** log password/token. |
| Secret | Credential SMTP lewat `dotnet user-secrets` / environment variable, bukan `appsettings.json` yang di-commit. Password admin seed dari config. |
| Validasi | Selalu di server (Data Annotations); client-side hanya bonus UX. |

---

## 10. UI / UX

Layout khusus auth (`_AuthLayout`) tanpa navbar & footer. Desktop: terbelah dua (panel gambar/branding kiri, form kanan); HP: form saja.

| Halaman | Route | Isi utama |
|---|---|---|
| Register | `/Account/Register` | 4 field, tombol "Daftar", link "Sudah punya akun? Masuk" |
| Login | `/Account/Login?returnUrl=` | Email, Password, Ingat saya, "Lupa password?", link "Daftar" |
| Forgot Password | `/Account/ForgotPassword` | Email, tombol "Kirim Link Reset", link kembali ke Login |
| Forgot Password Konfirmasi | hasil POST Forgot | Pesan generik, link kembali ke Login |
| Reset Password | `/Account/ResetPassword?token=` | Password baru + ulangi, hint "Minimal 8 karakter, kombinasi huruf & angka" |
| Access Denied | `/Account/AccessDenied` | Pesan 403 + tombol ke Home |

Ketentuan:
- `asp-validation-summary="ModelOnly"` untuk error umum; `asp-validation-for` per field.
- Flash message lewat `TempData["SuccessMessage"]` / `["ErrorMessage"]` dan partial `_FlashMessages`.
- `autocomplete` yang benar: `email`, `current-password`, `new-password`.
- Tombol submit di-disable setelah klik (cegah double-submit).
- Toggle "tampilkan password" pada input password.
- Aksesibilitas: label terhubung ke input, `aria-describedby` untuk hint, kontras WCAG AA, navigasi keyboard penuh.
- Navbar: belum login → "Masuk" & "Daftar"; sudah login → nama user + tombol "Keluar" (form POST).
- Teks UI Bahasa Indonesia santai, konsisten dengan pesan error di atas.

---

## 11. Konfigurasi & Registrasi di `Program.cs`

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
if (builder.Environment.IsDevelopment()) builder.Services.AddScoped<IEmailSender, FakeEmailSender>();
else                                      builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Account/AccessDenied";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.ExpireTimeSpan = TimeSpan.FromDays(14);
        o.SlidingExpiration = true;
        o.Events.OnValidatePrincipal = /* validasi SecurityStamp */;
    });
builder.Services.AddRateLimiter(/* fixed window untuk Login/Register/ForgotPassword */);

// urutan middleware
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();   // wajib sebelum UseAuthorization
app.UseAuthorization();
```

`appsettings.json` (tanpa secret):
```json
"Smtp": { "Host": "", "Port": 587, "FromAddress": "no-reply@{app}.com", "FromName": "{App}" },
"Auth": { "MinPasswordLength": 8, "MaxFailedAttempts": 5, "LockoutMinutes": 10, "ResetTokenHours": 1 }
```
Credential SMTP (`Smtp:User`, `Smtp:Password`) lewat user-secrets / env var.

---

## 12. Di Luar Scope Sekarang (v2 / Backlog)

1. Ganti password & edit profil saat login (`/Account/Profile`, `/Account/ChangePassword`).
2. Verifikasi email setelah register (akun `EmailConfirmed`).
3. 2FA (TOTP).
4. Login Google/Microsoft.
5. Halaman admin untuk kelola user & ubah role.
6. Audit log autentikasi di tabel sendiri.
7. "Logout dari semua perangkat" (memanfaatkan `SecurityStamp`).

---

## 13. Testing

### Unit test (xUnit, mock `IUserRepository` & `IEmailSender`)
| Kasus | Ekspektasi |
|---|---|
| Register email baru | `Success = true`, user tersimpan, `PasswordHash != password`, role default |
| Register email duplikat (beda huruf besar/kecil) | `Success = false`, "Email sudah terdaftar." |
| Login benar | `Success = true`, `SignIn` dipanggil dengan claim lengkap |
| Login password salah | `Success = false`, pesan generik, `FailedLoginCount` naik |
| Login email tak ada | Pesan sama persis dengan password salah |
| Login ke-5 salah | Akun terkunci; login benar saat terkunci tetap ditolak |
| Lockout sudah lewat | Login benar berhasil, counter reset |
| Forgot email ada | Token ter-hash tersimpan, `IEmailSender.SendAsync` dipanggil 1x |
| Forgot email tak ada | Tidak error, tidak kirim email, hasil identik |
| Reset token valid | Password berubah, token null, `SecurityStamp` berganti |
| Reset token kedaluwarsa / salah / sudah dipakai | `Success = false`, pesan generik |
| Mapping `RegisterDto → User` | Email ter-normalisasi, role default, role tidak berasal dari input |

### Test UI (browser headless)
- Alur: register → login → logout; login salah; forgot → reset → login dengan password baru.
- `returnUrl` eksternal (`https://evil.com`) tidak diikuti.
- Akses halaman admin sebagai User biasa → AccessDenied; tanpa login → redirect Login.
- Tampilan HP & desktop (layout terbelah vs form saja).

### Manual / security checklist
- Cookie punya flag HttpOnly + Secure + SameSite.
- Token reset tidak muncul di log; halaman reset mengirim `Referrer-Policy: no-referrer`.
- Reset password di tab A membuat sesi di tab B logout.

---

## 14. Definition of Done

- [ ] Fitur F1–F9 jalan end-to-end dan lolos acceptance criteria §5.
- [ ] Migration terbuat, `database update` sukses di DB kosong; seeder idempotent.
- [ ] Struktur folder & aturan dependensi §7 diikuti (tidak ada `using Repositories` di Controller).
- [ ] Unit test §13 hijau; test UI auth hijau.
- [ ] Tidak ada secret/password asli ter-commit; `appsettings.json` bersih.
- [ ] Tidak ada password/token di log.
- [ ] Checklist keamanan §9 dicentang satu per satu.
- [ ] README singkat: cara set user-secrets SMTP dan akun admin seed.

---

## 15. Asumsi & Hal yang Perlu Dikonfirmasi

1. Auth pakai **cookie** (MVC server-rendered), bukan JWT. Kalau nanti ada API/mobile, tambah JWT sebagai skema kedua.
2. Email dikirim via SMTP (MailKit). Untuk SendGrid/Resend cukup ganti implementasi `IEmailSender`.
3. Nama role default (`User`) disesuaikan dengan domain.
4. Verifikasi email belum wajib di MVP (risiko akun dengan email palsu diterima).
5. Bahasa UI: Indonesia santai.
6. Satu akun = satu email unik; tidak ada multi-akun per email.
