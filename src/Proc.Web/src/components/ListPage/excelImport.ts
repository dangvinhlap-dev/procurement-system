/*
 * Kiểu dữ liệu và nguồn nhập dùng chung cho hộp thoại Nhập Excel (ExcelImportDialog) – tách khỏi file component để fast refresh hoạt động.
 */

/** Một lỗi nhập Excel – khớp Proc.Application.Excel.ExcelImportError. */
export interface ImportError {
  row: number
  code: string | null
  message: string
}

/** Phần chung của mọi kết quả nhập Excel: số dòng, lỗi theo dòng, đã lưu hay chưa. */
export interface ImportOutcome {
  totalRows: number
  errors: ImportError[]
  committed: boolean
}

/** Kết quả nhập danh mục – khớp Proc.Application.Excel.ExcelImportResult. */
interface CatalogImportResult extends ImportOutcome {
  catalog: string
  created: number
  updated: number
}

/**
 * Nơi nhập file: đường dẫn API, file mẫu, lời hướng dẫn và câu tóm tắt kết quả. Danh mục dùng {@link catalogImportSource};
 * dữ liệu giao dịch (ví dụ dữ liệu mua lịch sử) tự khai báo vì kết quả khác (không có "cập nhật").
 */
export interface ExcelImportSource<R extends ImportOutcome> {
  /** API nhận file, chưa gồm `?commit=`. */
  importUrl: string
  /** API tải file mẫu. */
  templateUrl: string
  /** Tên file mẫu khi server không gửi tên. */
  templateFileName: string
  /** Lời hướng dẫn đầu hộp thoại. */
  hint: string
  /** Câu báo khi file hợp lệ (chưa lưu) và khi đã lưu. */
  /* Khai báo kiểu phương thức (không phải thuộc tính hàm) để nguồn của từng màn hình gán được vào ExcelImportSource<ImportOutcome>. */
  summary(result: R): { valid: string; committed: string }
}

/**
 * Nguồn nhập của một danh mục (design.md mục 5.10): /api/excel/{catalog}, nhập theo mã – có dòng mới và dòng cập nhật.
 * @param catalog Mã danh mục trên API, ví dụ "items".
 */
export function catalogImportSource(catalog: string): ExcelImportSource<CatalogImportResult> {
  return {
    importUrl: `/api/excel/${catalog}/import`,
    templateUrl: `/api/excel/${catalog}?template=true`,
    templateFileName: `mau-${catalog}.xlsx`,
    hint:
      'Mã đã có thì cập nhật, chưa có thì thêm mới; không xóa bản ghi nào. Bấm "Kiểm tra thử" trước – "Nhập thật" chỉ lưu khi mọi ' +
      'dòng đều đúng. Dễ nhất: bấm "Xuất Excel" trên danh sách, sửa file đó rồi nhập lại.',
    summary: (r) => ({
      valid: `File hợp lệ: ${r.totalRows} dòng (${r.created} dòng mới, ${r.updated} dòng cập nhật). Bấm "Nhập thật" để lưu.`,
      committed: `Đã nhập xong ${r.totalRows} dòng: ${r.created} dòng mới, ${r.updated} dòng cập nhật.`,
    }),
  }
}
