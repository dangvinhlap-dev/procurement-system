/**
 * Chép chữ vào bộ nhớ tạm. Trình duyệt chỉ cho `navigator.clipboard` trên https/localhost; máy chủ nội bộ chạy http thì dùng cách cũ
 * (ô textarea tạm + `execCommand('copy')`) để vẫn sao chép được.
 * @param text Chữ cần chép.
 * @returns Có chép được không.
 */
export async function copyText(text: string): Promise<boolean> {
  try {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(text)
      return true
    }
  } catch {
    // Bị chặn quyền – thử cách cũ bên dưới.
  }
  const area = document.createElement('textarea')
  area.value = text
  area.setAttribute('readonly', '')
  area.style.position = 'fixed'
  area.style.opacity = '0'
  document.body.appendChild(area)
  area.select()
  try {
    return document.execCommand('copy')
  } catch {
    return false
  } finally {
    area.remove()
  }
}
