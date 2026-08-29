import { z } from 'zod'

export const productSchema = z.object({
  name: z.string().min(1, 'Name is required').max(200),
  sku: z.string().min(1, 'SKU is required').max(50),
  barcode: z.string().optional(),
  categoryId: z.string().optional(),
  supplierId: z.string().optional(),
  sellingPrice: z.number().min(0, 'Must be 0 or more'),
  costPrice: z.number().min(0, 'Must be 0 or more'),
  minimumStock: z.number().int().min(0),
  trackInventory: z.boolean(),
  initialQuantity: z.number().int().min(0),
})

export type ProductFormValues = z.infer<typeof productSchema>

export const productDefaults: ProductFormValues = {
  name: '',
  sku: '',
  barcode: '',
  categoryId: '',
  supplierId: '',
  sellingPrice: 0,
  costPrice: 0,
  minimumStock: 0,
  trackInventory: true,
  initialQuantity: 0,
}
