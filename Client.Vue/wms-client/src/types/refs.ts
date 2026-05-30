export interface ProductRef {
  id: string
  sku: string
  name: string
}

export interface LocationRef {
  id: string
  code: string
  address: string
}

export interface LotRef {
  id: string
  number: string
  expirationDate: string | null
}
