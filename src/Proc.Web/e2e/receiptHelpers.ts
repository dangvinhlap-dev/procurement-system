import { expect, type APIRequestContext } from '@playwright/test'
import { api, approveSupplier, unique } from './inventoryHelpers.ts'

/** Hàm dùng chung cho test nhận hàng bằng phiếu xác nhận nhận hàng XNH (lát K2, design.md mục 19.4). */

/** Ngày hôm nay giờ Việt Nam, dạng yyyy-MM-dd. */
export const todayVn = () => new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })

/**
 * Tạo một quản trị mới tại BHA rồi duyệt PO bằng tài khoản đó (người lập không tự duyệt; mỗi cấp/chặng một người – lát R3).
 * @param request Ngữ cảnh API của Playwright.
 * @param bha API quản trị tại BHA.
 * @param poId Id PO đang chờ duyệt.
 */
export async function approveAsNewAdmin(request: APIRequestContext, bha: Awaited<ReturnType<typeof api>>, poId: string) {
  const roles = await bha.get<{ id: string; code: string }[]>('/api/roles')
  const userName = unique('dy').toLowerCase()
  await bha.send('post', '/api/users', {
    userName, fullName: `Người duyệt ${userName}`, email: null, password: 'Demo@2026',
    access: { roleIds: [roles.find((r) => r.code === 'ADMIN')!.id], warehouseIds: [], itemCategoryIds: [] },
  })
  const login = await request.post('/api/auth/login', { data: { userName, password: 'Demo@2026' } })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { companies: { id: string; code: string }[] } }
  const approved = await request.post(`/api/purchase-orders/${poId}/approve`, {
    headers: { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.companies.find((c) => c.code === 'BHA')!.id },
    data: { comment: null },
  })
  expect(approved.ok(), await approved.text()).toBeTruthy()
}

/**
 * Tạo mặt hàng + NCC áp dụng tại BHA và một PO đã duyệt (qty HOP × 50.000, VAT 10%, kho nhận KT, hẹn giao hôm nay) – chưa nhận hàng.
 * @param request Ngữ cảnh API của Playwright.
 * @param qty SL đặt.
 * @returns PO (Id, số, Id dòng), mặt hàng, NCC.
 */
export async function seedApprovedPo(request: APIRequestContext, qty = 10) {
  const head = await api(request)
  const uoms = await head.get<{ id: string; code: string }[]>('/api/uoms')
  const hop = uoms.find((u) => u.code === 'HOP')!.id
  const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const category = await head.send<{ id: string }>('post', '/api/item-categories', {
    code: null, name: `Nhóm nhận hàng ${crypto.randomUUID().slice(0, 8)}`, parentId: null, inventoryAccount: null,
  })
  const item = await head.send<{ id: string; code: string }>('post', '/api/items', {
    code: null, name: 'Găng tay nhận hàng', englishName: null, type: 'Inventory', categoryId: category.id, baseUomId: hop,
    specification: null, brand: null, origin: null, barcode: null, vatCode: '10', technicalSpecs: null, trackLot: false,
    trackExpiry: false, minStock: null, maxStock: null, conversions: [],
  })
  const supplier = await head.send<{ id: string; code: string }>('post', '/api/suppliers', {
    code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name: `NCC nhận hàng ${unique('X')}`, shortName: null,
    isForeign: false, taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
    rating: null, status: 'Approved', statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
  })
  await approveSupplier(request, supplier.id)
  const bha = await api(request, 'BHA')
  await bha.send('put', `/api/suppliers/${supplier.id}/company`, { localCode: null, startDate: null, isActive: true })
  const kt = (await bha.get<{ id: string; code: string }[]>('/api/warehouses')).find((w) => w.code === 'KT')!.id
  const po = await bha.send<{ id: string; no: string; lines: { id: string }[] }>('post', '/api/purchase-orders', {
    date: todayVn(), supplierId: supplier.id, warehouseId: kt, expectedDate: todayVn(), paymentTerms: null, deliveryTerms: null,
    deliveryAddress: null, receiveTolerancePct: 0, note: null,
    lines: [{ id: null, itemId: item.id, uomId: hop, qty, unitPrice: 50000, discountPct: 0, vatCode: '10', deliveryDate: null, note: null, sourcePrLineId: null }],
  })
  await bha.send('post', `/api/purchase-orders/${po.id}/submit`, {})
  // Cấp PO thường và chặng "Ngoài danh sách/giá" (POL) – mỗi chặng một người duyệt.
  for (let i = 0; i < 2; i++) await approveAsNewAdmin(request, bha, po.id)
  return { bha, po, itemId: item.id, itemCode: item.code, supplierId: supplier.id, supplierCode: supplier.code, hop, kt }
}

/**
 * Nhận hàng qua API: lập XNH (đạt hết SL, kết quả Đạt) cho dòng 1 của PO rồi xác nhận.
 * @param bha API quản trị tại BHA.
 * @param poId Id PO đã duyệt.
 * @param poLineId Id dòng PO.
 * @param qty SL nhận.
 * @returns Phiếu XNH đã xác nhận.
 */
export async function receiveByApi(bha: Awaited<ReturnType<typeof api>>, poId: string, poLineId: string, qty: number) {
  const xnh = await bha.send<{ id: string; no: string }>('post', '/api/receipt-confirmations', {
    purchaseOrderId: poId, date: todayVn(), deliveryNoteNo: null, deliverer: null, note: null, invoiceNo: null, invoiceSeries: null,
    invoiceDate: null, invoiceAmount: null,
    lines: [{ purchaseOrderLineId: poLineId, deliveredQty: qty, acceptedQty: qty, rejectedQty: 0, checkResult: 'Passed', checkNote: null, note: null }],
  })
  return bha.send<{ id: string; no: string; lines: { id: string }[] }>('post', `/api/receipt-confirmations/${xnh.id}/confirm`, {})
}
