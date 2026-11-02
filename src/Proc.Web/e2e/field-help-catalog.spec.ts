import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, openByCommand, unique } from './inventoryHelpers.ts'

/**
 * F1 trên danh mục (design.md mục 9.3a): ô của lưới danh sách NCC, ô của ngăn kéo NCC, ô của dòng con (người liên hệ – bản ghi riêng,
 * không lẫn với tên NCC) và "Lịch sử thay đổi" của ngăn kéo gồm cả người liên hệ.
 */
test('F1 trên lưới danh sách, ngăn kéo và dòng người liên hệ của NCC', async ({ page, request }) => {
  const head = await api(request)
  const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const name = `NCC lịch sử ô ${unique('F')}`
  const supplier = await head.send<{ id: string; code: string }>('post', '/api/suppliers', {
    code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name, shortName: null,
    isForeign: false, taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
    rating: null, status: 'Potential', statusNote: null, internalCompanyId: null,
    contacts: [{ id: null, name: 'Trần Liên Hệ', position: 'Kế toán', phone: null, email: null, isPrimary: true }],
    bankAccounts: [],
  })

  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'SUP')
  await page.locator('[data-list-search]').fill(supplier.code)

  // F1 trên ô "Tên nhà cung cấp" của lưới danh sách.
  const row = page.locator('.data-grid-row', { hasText: supplier.code })
  await row.locator('[data-audit-field="name"]').hover()
  await page.keyboard.press('F1')
  const listHelp = page.getByRole('dialog', { name: 'Trợ giúp ô: Tên nhà cung cấp' })
  await expect(listHelp.locator('.ant-table-row').first()).toContainText('Tạo')
  await expect(listHelp.locator('.ant-table-row').first()).toContainText(name)
  await page.keyboard.press('Escape')
  await expect(listHelp).toBeHidden()

  // Ngăn kéo: ô đầu và ô của người liên hệ (bản ghi riêng).
  await row.dblclick()
  const drawer = page.locator('.detail-drawer')
  await drawer.getByLabel('Tên nhà cung cấp').focus()
  await page.keyboard.press('F1')
  const drawerHelp = page.getByRole('dialog', { name: 'Trợ giúp ô: Tên nhà cung cấp' })
  await expect(drawerHelp.locator('.ant-table-row').first()).toContainText(name)
  await page.keyboard.press('Escape')
  await expect(drawerHelp).toBeHidden()

  await drawer.getByRole('tab', { name: /Người liên hệ/i }).click()
  await drawer.getByLabel('Họ tên').focus()
  await page.keyboard.press('F1')
  const contactHelp = page.getByRole('dialog', { name: 'Trợ giúp ô: Họ tên' })
  await expect(contactHelp.locator('.ant-table-row').first()).toContainText('Trần Liên Hệ')
  await contactHelp.getByRole('tab', { name: /Thông tin ô/i }).click()
  await expect(contactHelp).toContainText('SupplierContact.Name')
  await page.keyboard.press('Escape')
  await expect(contactHelp).toBeHidden()

  // Lịch sử cả NCC: có cả dòng người liên hệ.
  await drawer.getByRole('button', { name: 'Lịch sử thay đổi' }).click()
  const all = page.getByRole('dialog', { name: /Lịch sử thay đổi/ })
  await expect(all.locator('.ant-table-row', { hasText: 'Trần Liên Hệ' })).toContainText('Người liên hệ NCC')
  await page.screenshot({ path: 'e2e/screenshots/record-history-supplier.png' })
})
