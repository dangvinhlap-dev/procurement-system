#!/usr/bin/env node
/**
 * Tạo dữ liệu thử cho môi trường test (KHÔNG chạy trên CSDL thật): bộ phận, vai trò, tài khoản, xếp loại NCC, quy trình duyệt PO, nhà cung cấp
 * (có duyệt hồ sơ bằng tài khoản khác người lập), NCC nội bộ, vật tư, NCC – sản phẩm (khai ở tổng công ty). Dữ liệu lấy từ du-lieu.json cùng thư mục.
 *
 * Gọi API của phần mềm nên mọi quy tắc nghiệp vụ được kiểm tra như khi nhập tay. Chạy lại không tạo trùng: mục đã có (theo mã/tên) thì bỏ qua.
 * Công ty, kho, ĐVT, nhóm hàng, nhóm NCC, đánh số danh mục, kỳ kho tháng hiện tại do bộ khởi tạo của ứng dụng tạo (mục Seed trong
 * appsettings.Production.json – xem README.md cùng thư mục).
 *
 * Cách chạy (Node.js 22 trở lên, Windows hoặc Linux):
 *   node tao-du-lieu-thu.mjs --url http://192.168.1.199:5006 --user admin --password <mật khẩu admin> [--out ket-qua.json]
 */
import { readFileSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const args = Object.fromEntries(process.argv.slice(2).reduce((acc, v, i, all) => (v.startsWith('--') ? [...acc, [v.slice(2), all[i + 1]]] : acc), []))
const baseUrl = (args.url ?? 'http://localhost:5000').replace(/\/$/, '')
const here = dirname(fileURLToPath(import.meta.url))
const data = JSON.parse(readFileSync(join(here, 'du-lieu.json'), 'utf8'))
const result = { suppliers: {}, internal: {}, items: {}, users: [] }

/** Phiên làm việc của một tài khoản: gọi API trong một công ty (header X-Company-Id). */
async function login(userName, password) {
  const res = await fetch(`${baseUrl}/api/auth/login`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userName, password }),
  })
  if (!res.ok) throw new Error(`Không đăng nhập được ${userName}: ${res.status} ${await res.text()}`)
  const body = await res.json()
  const companies = Object.fromEntries(body.user.companies.map((c) => [c.code, c.id]))
  const call = async (method, url, company, payload) => {
    const r = await fetch(`${baseUrl}${url}`, {
      method,
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${body.accessToken}`, 'X-Company-Id': companies[company] },
      body: payload === undefined ? undefined : JSON.stringify(payload),
    })
    const text = await r.text()
    if (!r.ok) throw new Error(`${method} ${url} (${company}): ${r.status} ${text}`)
    return text ? JSON.parse(text) : null
  }
  return {
    companies,
    get: (url, company = 'TCT') => call('GET', url, company),
    post: (url, payload, company = 'TCT') => call('POST', url, company, payload ?? {}),
    put: (url, payload, company = 'TCT') => call('PUT', url, company, payload),
  }
}

const log = (msg) => console.log(msg)
const byCode = (list) => Object.fromEntries(list.map((x) => [x.code, x]))
/** Danh sách có giới hạn (hàng hóa, NCC) trả về { items, truncated } – lấy mảng. */
const rows = (x) => (Array.isArray(x) ? x : x.items)

const admin = await login(args.user ?? 'admin', args.password ?? '')
const companyCodes = data.congTy.map((c) => c.ma)
for (const c of companyCodes) if (!admin.companies[c]) throw new Error(`Chưa có công ty ${c} – kiểm tra mục Seed của appsettings.`)

// 1. Bộ phận mỗi công ty.
for (const c of companyCodes) {
  const existing = byCode(await admin.get('/api/departments', c))
  for (const d of data.boPhan) {
    if (existing[d.ma]) continue
    await admin.post('/api/departments', { code: d.ma, name: d.ten, managerName: d.truongBoPhan }, c)
  }
}
log('✓ Bộ phận')

// 2. Vai trò: bộ vai trò mẫu Ban Cung ứng + vai trò toàn quyền.
await admin.post('/api/roles/templates')
let roles = byCode(await admin.get('/api/roles'))
if (!roles[data.vaiTroToanQuyen.ma]) {
  const catalog = await admin.get('/api/permissions')
  const codes = catalog.flatMap((g) => (g.permissions ?? [g]).map((p) => p.code)).filter(Boolean)
  await admin.post('/api/roles', { code: data.vaiTroToanQuyen.ma, name: data.vaiTroToanQuyen.ten, permissionCodes: codes })
  roles = byCode(await admin.get('/api/roles'))
}
log('✓ Vai trò')

// 3. Tài khoản: mỗi công ty × vai trò; "toàn tập đoàn" của TCT được gán thêm ở APH, BMI.
const warehouses = Object.fromEntries(await Promise.all(companyCodes.map(async (c) => [c, byCode(await admin.get('/api/warehouses', c))])))
const categories = byCode(await admin.get('/api/item-categories'))
const accounts = []
for (const c of data.congTy) {
  for (const a of data.taiKhoan) {
    accounts.push({
      userName: `${c.ma.toLowerCase()}.${a.hauTo}`, fullName: `${a.ten} – ${c.ma}`, role: a.vaiTro,
      companies: a.toanTapDoan && c.laTongCongTy ? companyCodes : [c.ma], warehouses: [], categories: [],
    })
  }
}
for (const a of data.taiKhoanPhamVi) {
  accounts.push({ userName: a.tenDangNhap, fullName: a.ten, role: a.vaiTro, companies: [a.congTy], warehouses: a.kho ?? [], categories: a.nhomHang ?? [] })
}
const existingUsers = new Map((await admin.get('/api/users')).map((u) => [u.userName, u]))
for (const a of accounts) {
  const access = (company) => ({
    roleIds: [roles[a.role].id],
    warehouseIds: a.warehouses.map((w) => warehouses[company][w].id),
    itemCategoryIds: a.categories.map((k) => categories[k].id),
  })
  let user = existingUsers.get(a.userName)
  const [first, ...others] = a.companies
  if (!user) {
    user = await admin.post('/api/users', { userName: a.userName, fullName: a.fullName, email: null, password: data.matKhau, access: access(first) }, first)
  }
  for (const c of others) await admin.put(`/api/users/${user.id}/access`, access(c), c)
  result.users.push({ userName: a.userName, role: a.role, companies: a.companies })
}
log(`✓ ${accounts.length} tài khoản`)

// 4. Xếp loại NCC (dùng chung, làm ở tổng công ty).
const ratings = byCode(await admin.get('/api/supplier-rating-rules'))
for (const x of data.xepLoaiNcc) {
  if (ratings[x.ma]) continue
  await admin.post('/api/supplier-rating-rules', { code: x.ma, name: x.ten, sortOrder: x.thuTu, minOnTimePct: x.dungHanToiThieu, maxReturnPct: x.traHangToiDa })
}
log('✓ Xếp loại NCC')

// 5. Quy trình duyệt PO ở công ty thành viên: cấp 1 người duyệt toàn quyền, cấp 2 Trưởng ban từ 200 triệu (gồm VAT).
for (const c of data.quyTrinhDuyetPoCongTy) {
  const rules = await admin.get('/api/approval-rules', c)
  for (const r of data.quyTrinhDuyetPo) {
    if (rules.some((x) => x.docType === 'PO' && x.level === r.cap)) continue
    await admin.post('/api/approval-rules', { docType: 'PO', level: r.cap, minAmount: r.tuSoTien, roleId: roles[r.vaiTro].id }, c)
  }
}
log('✓ Quy trình duyệt PO')

// 6. Vật tư (danh mục dùng chung, tạo ở tổng công ty). Khóa kết quả "<công ty>:<thứ tự trong công ty>".
const uoms = byCode(await admin.get('/api/uoms'))
const existingItems = new Map(rows(await admin.get('/api/items')).map((i) => [i.name, i]))
const counters = {}
for (const v of data.vatTu) {
  const idx = (counters[v.congTy] = (counters[v.congTy] ?? -1) + 1)
  let item = existingItems.get(v.ten)
  if (!item) {
    item = await admin.post('/api/items', {
      code: null, name: v.ten, englishName: null, type: 'Inventory', categoryId: categories[v.nhom].id, baseUomId: uoms[v.dvt].id,
      specification: null, brand: null, origin: null, barcode: null, vatCode: v.thue, technicalSpecs: null,
      trackLot: !!v.theoLo, trackExpiry: !!v.hanDung, minStock: v.tonMin ?? null, maxStock: v.tonMax ?? null,
      conversions: (v.quyDoi ?? []).map(([u, f]) => ({ uomId: uoms[u].id, factor: f })),
      safetyStock: v.tonAnToan ?? null, reorderPoint: v.diemDatLai ?? null, isCritical: !!v.trongYeu,
    })
  }
  result.items[`${v.congTy}:${idx}`] = { id: item.id, code: item.code, name: v.ten, uom: v.dvt }
}
log(`✓ ${data.vatTu.length} vật tư`)

// 7. Nhà cung cấp: người lập là admin, người duyệt hồ sơ là tct.hoidong (người lập không tự duyệt – design.md mục 18.6).
const approver = await login('tct.hoidong', data.matKhau)
const approvePending = async (supplierId) => {
  const pending = await approver.get(`/api/supplier-change-requests?supplierId=${supplierId}`)
  for (const r of pending.filter((x) => x.status === 'Submitted')) await approver.post(`/api/supplier-change-requests/${r.id}/approve`, { comment: null })
}
const groups = byCode(await admin.get('/api/supplier-groups'))
const existingSuppliers = new Map(rows(await admin.get('/api/suppliers')).map((s) => [s.name, s]))
const supplierBody = (s, status, extra = {}) => ({
  code: null, groupId: groups[s.nhom].id, name: s.ten, shortName: s.tenNgan ?? null, isForeign: !!s.nuocNgoai, taxCode: s.mst ?? null,
  address: s.diaChi ?? null, phone: '028 3999 0000', email: null, website: null, paymentTerms: 'Thanh toán 30 ngày sau khi nhận hàng và hóa đơn',
  supplyScope: s.phamVi ?? null, rating: null, status, statusNote: null, internalCompanyId: null,
  contacts: [{ id: null, name: `Liên hệ ${s.tenNgan ?? s.ten}`, position: 'Kinh doanh', phone: '0909 000 000', email: null, isPrimary: true }],
  bankAccounts: s.nuocNgoai ? [] : [{ id: null, bankName: 'Ngân hàng Thử nghiệm', branch: 'CN TP.HCM', accountNumber: `0011${s.mst.slice(-6)}`, accountName: s.ten, isDefault: true }],
  ...extra,
})
const applyTo = async (supplierId, companies) => {
  for (const c of companies) await admin.put(`/api/suppliers/${supplierId}/company`, { localCode: null, startDate: null, isActive: true }, c)
}
for (const s of data.nhaCungCap) {
  let supplier = existingSuppliers.get(s.ten)
  const target = s.trangThai ?? 'Approved'
  if (!supplier) {
    supplier = await admin.post('/api/suppliers', supplierBody(s, target === 'Potential' ? 'Potential' : 'Approved'))
    await approvePending(supplier.id)
    if (target !== 'Potential') await applyTo(supplier.id, s.dung)
  }
  // NCC tạm ngưng: duyệt trước rồi chuyển Tạm ngưng (bắt buộc lý do) – để test chặn lập PO. Chạy lại thì chỉ làm khi chưa tạm ngưng.
  if (target === 'Suspended') {
    const detail = await admin.get(`/api/suppliers/${supplier.id}`)
    if (detail.status !== 'Suspended') {
      await admin.put(`/api/suppliers/${supplier.id}`, supplierBody(s, 'Suspended',
        { statusNote: 'Giao hàng trễ nhiều lần – tạm ngưng để kiểm tra (dữ liệu thử)', contacts: detail.contacts, bankAccounts: detail.bankAccounts }))
      await approvePending(supplier.id)
    }
  }
  result.suppliers[s.khoa] = { id: supplier.id, code: supplier.code, name: s.ten }

  // NCC – sản phẩm theo danh sách "vatTu" (khóa <công ty>:<thứ tự>). Danh mục dùng chung nên chỉ khai ở tổng công ty.
  if (target !== 'Approved') continue
  const linked = new Set(rows(await admin.get(`/api/suppliers/${supplier.id}/items`)).map((x) => x.itemId))
  for (const [k, key] of s.vatTu.entries()) {
    const item = result.items[key]
    if (!item || linked.has(item.id)) continue
    await admin.post(`/api/suppliers/${supplier.id}/items`, {
      itemId: item.id, supplierItemCode: null, purchaseUomId: uoms[item.uom].id, moq: null, leadTimeDays: 3 + k * 2, isPreferred: k === 0,
    })
    linked.add(item.id)
  }
}
log(`✓ ${data.nhaCungCap.length} nhà cung cấp`)

// 8. NCC nội bộ: mỗi công ty một NCC nhóm Nội bộ liên kết công ty đó (mua bán liên công ty ICS/ICR – design.md mục 6.3).
for (const c of data.congTy) {
  const name = `NCC nội bộ – ${c.ten}`
  let supplier = existingSuppliers.get(name)
  if (!supplier) {
    supplier = await admin.post('/api/suppliers', supplierBody({ nhom: 'NB', ten: name, tenNgan: c.ma, mst: c.mst, phamVi: 'Mua bán liên công ty' }, 'Approved',
      { internalCompanyId: admin.companies[c.ma] }))
    await approvePending(supplier.id)
    await applyTo(supplier.id, companyCodes)
  }
  result.internal[c.ma] = supplier.code
}
log('✓ NCC nội bộ')

if (args.out) writeFileSync(args.out, JSON.stringify(result, null, 2))
log(`Xong. Đăng nhập ${baseUrl} – mật khẩu tài khoản test: ${data.matKhau}`)
