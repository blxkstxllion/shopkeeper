import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Input, FormField } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'
import { createProduct, getProductCategories, updateProduct, uploadProductImage } from '@/api/products'
import { getSuppliers } from '@/api/suppliers'
import { resolveUploadUrl } from '@/lib/format'
import type { ApiErrorPayload } from '@/types/auth'
import type { Product } from '@/types/product'
import { AxiosError } from 'axios'
import { Package, Upload, X } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { productDefaults, productSchema, type ProductFormValues } from './product.schema'

const MAX_IMAGE_BYTES = 5 * 1024 * 1024
const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/gif']

export function ProductFormModal({
  isOpen,
  onClose,
  branchId,
  product,
}: {
  isOpen: boolean
  onClose: () => void
  branchId: string | null
  product?: Product | null
}) {
  const queryClient = useQueryClient()
  const [serverError, setServerError] = useState<string | null>(null)
  const [imageUrl, setImageUrl] = useState<string | null>(null)
  const [isUploadingImage, setIsUploadingImage] = useState(false)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const isEditing = Boolean(product)

  const { data: categories } = useQuery({ queryKey: ['product-categories'], queryFn: getProductCategories })
  const { data: suppliers } = useQuery({ queryKey: ['suppliers'], queryFn: getSuppliers })

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    reset,
  } = useForm<ProductFormValues>({
    resolver: zodResolver(productSchema),
    values: product
      ? {
          name: product.name,
          sku: product.sku,
          barcode: product.barcode ?? '',
          categoryId: product.categoryId ?? '',
          supplierId: product.supplierId ?? '',
          sellingPrice: product.sellingPrice,
          costPrice: product.costPrice,
          minStock: product.minStock,
          reorderLevel: product.reorderLevel,
          trackInventory: product.trackInventory,
          initialQuantity: 0,
        }
      : productDefaults,
  })

  // Resets the image preview whenever a different product is opened for editing (or the
  // modal reopens for a fresh "Add product") - without this, the previous product's image
  // would keep showing after switching.
  useEffect(() => {
    if (isOpen) {
      setImageUrl(product?.imageUrl ?? null)
      setServerError(null)
    }
  }, [isOpen, product])

  const mutation = useMutation({
    mutationFn: async (values: ProductFormValues) => {
      const payload = {
        name: values.name,
        sku: values.sku,
        barcode: values.barcode || null,
        categoryId: values.categoryId || null,
        supplierId: values.supplierId || null,
        imageUrl,
        sellingPrice: values.sellingPrice,
        costPrice: values.costPrice,
        minStock: values.minStock,
        reorderLevel: values.reorderLevel,
        trackInventory: values.trackInventory,
        initialQuantity: values.initialQuantity,
        branchId,
      }
      if (isEditing && product) {
        await updateProduct({ ...payload, id: product.id, isActive: product.isActive })
      } else {
        await createProduct(payload)
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products'] })
      reset(productDefaults)
      setImageUrl(null)
      onClose()
    },
    onError: (err) => {
      const apiErr = (err as AxiosError<ApiErrorPayload>).response?.data
      setServerError(apiErr?.title ?? 'Unable to save this product. Please try again.')
    },
  })

  async function handleFileSelected(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = '' // allow re-selecting the same file later
    if (!file) return

    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
      setServerError('Please choose a JPEG, PNG, WEBP, or GIF image.')
      return
    }
    if (file.size > MAX_IMAGE_BYTES) {
      setServerError('Images must be 5MB or smaller.')
      return
    }

    setServerError(null)
    setIsUploadingImage(true)
    try {
      const { url } = await uploadProductImage(file)
      setImageUrl(url)
    } catch (err) {
      const apiErr = (err as AxiosError<ApiErrorPayload>).response?.data
      setServerError(apiErr?.title ?? 'Unable to upload that image. Please try again.')
    } finally {
      setIsUploadingImage(false)
    }
  }

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={isEditing ? 'Edit product' : 'Add product'} size="lg">
      <form onSubmit={handleSubmit((values) => mutation.mutate(values))} className="flex flex-col gap-4">
        {serverError && <Alert tone="error">{serverError}</Alert>}

        <div className="flex items-center gap-4">
          <div className="flex h-20 w-20 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-slate-200 bg-slate-50 dark:border-slate-700 dark:bg-slate-800">
            {imageUrl ? (
              <img src={resolveUploadUrl(imageUrl)} alt="Product" className="h-full w-full object-cover" />
            ) : (
              <Package className="h-8 w-8 text-slate-300 dark:text-slate-600" />
            )}
          </div>
          <div className="flex flex-col gap-1.5">
            <input
              ref={fileInputRef}
              type="file"
              accept={ALLOWED_IMAGE_TYPES.join(',')}
              className="hidden"
              onChange={handleFileSelected}
            />
            <div className="flex gap-2">
              <Button
                type="button"
                variant="secondary"
                size="sm"
                isLoading={isUploadingImage}
                onClick={() => fileInputRef.current?.click()}
              >
                <Upload className="h-3.5 w-3.5" />
                {imageUrl ? 'Change photo' : 'Upload photo'}
              </Button>
              {imageUrl && (
                <Button type="button" variant="ghost" size="sm" onClick={() => setImageUrl(null)}>
                  <X className="h-3.5 w-3.5" />
                  Remove
                </Button>
              )}
            </div>
            <p className="text-xs text-slate-400">JPEG, PNG, WEBP, or GIF. Up to 5MB.</p>
          </div>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <FormField label="Name" htmlFor="name" error={errors.name?.message}>
            <Input id="name" {...register('name')} error={errors.name?.message} />
          </FormField>
          <FormField label="SKU" htmlFor="sku" error={errors.sku?.message}>
            <Input id="sku" {...register('sku')} error={errors.sku?.message} />
          </FormField>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <FormField label="Barcode (optional)" htmlFor="barcode">
            <Input id="barcode" {...register('barcode')} />
          </FormField>
          <FormField label="Category (optional)" htmlFor="categoryId">
            <select
              id="categoryId"
              {...register('categoryId')}
              className="h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-500/40 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
            >
              <option value="">No category</option>
              {categories?.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </FormField>
        </div>

        <FormField label="Supplier (optional)" htmlFor="supplierId">
          <select
            id="supplierId"
            {...register('supplierId')}
            className="h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-500/40 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
          >
            <option value="">No supplier</option>
            {suppliers?.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </select>
        </FormField>

        <div className="grid grid-cols-2 gap-3">
          <FormField label="Selling price" htmlFor="sellingPrice" error={errors.sellingPrice?.message}>
            <Input
              id="sellingPrice"
              type="number"
              step="0.01"
              {...register('sellingPrice', { valueAsNumber: true })}
              error={errors.sellingPrice?.message}
            />
          </FormField>
          <FormField label="Cost price" htmlFor="costPrice" error={errors.costPrice?.message}>
            <Input
              id="costPrice"
              type="number"
              step="0.01"
              {...register('costPrice', { valueAsNumber: true })}
              error={errors.costPrice?.message}
            />
          </FormField>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <FormField label="Minimum stock" htmlFor="minStock" error={errors.minStock?.message}>
            <Input
              id="minStock"
              type="number"
              {...register('minStock', { valueAsNumber: true })}
              error={errors.minStock?.message}
            />
          </FormField>
          <FormField label="Reorder level" htmlFor="reorderLevel" error={errors.reorderLevel?.message}>
            <Input
              id="reorderLevel"
              type="number"
              {...register('reorderLevel', { valueAsNumber: true })}
              error={errors.reorderLevel?.message}
            />
          </FormField>
        </div>

        <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-300">
          <input
            type="checkbox"
            {...register('trackInventory')}
            className="h-4 w-4 rounded border-slate-300 text-primary-600"
          />
          Track inventory for this product
        </label>

        {!isEditing && (
          <FormField
            label="Initial quantity"
            htmlFor="initialQuantity"
            hint={branchId ? undefined : 'No branch selected yet.'}
          >
            <Input id="initialQuantity" type="number" {...register('initialQuantity', { valueAsNumber: true })} />
          </FormField>
        )}

        <div className="mt-2 flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" isLoading={isSubmitting || mutation.isPending} disabled={isUploadingImage}>
            {isEditing ? 'Save changes' : 'Add product'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
