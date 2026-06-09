<script setup lang="ts">
import { computed, provide, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useConfirm } from 'primevue/useconfirm'
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import ProgressSpinner from 'primevue/progressspinner'
import PageHeader from '@/components/common/PageHeader.vue'
import RefreshButton from '@/components/common/RefreshButton.vue'
import CategoryTreeNode from './CategoryTreeNode.vue'
import {
  useCategoryTree,
  useCreateCategory,
  useDeleteCategory,
  useUpdateCategory,
} from './useCategories'
import { categoryTreeKey } from './category-tree-context'
import { useAuthStore } from '@/stores/auth'
import type { CategoryTreeNode as TreeNode } from '@/types/categories'

const router = useRouter()
const auth = useAuthStore()
const confirm = useConfirm()
const toast = useToast()

const { data: tree, isLoading, isFetching, isError, error, refetch } = useCategoryTree()

const create = useCreateCategory()
const update = useUpdateCategory()
const del = useDeleteCategory()

const isEmpty = computed(() => !isLoading.value && (tree.value?.length ?? 0) === 0)

// --- Expansion state (default: everything expanded) -------------------------
const collapsed = ref<Set<string>>(new Set())
function isCollapsed(id: string) {
  return collapsed.value.has(id)
}
function toggle(id: string) {
  const next = new Set(collapsed.value)
  next.has(id) ? next.delete(id) : next.add(id)
  collapsed.value = next
}

// --- Drag & drop re-parenting ----------------------------------------------
const draggingId = ref<string | null>(null)
const dragOverId = ref<string | null>(null)
const rootDragOver = ref(false)

function findNode(nodes: TreeNode[], id: string): TreeNode | null {
  for (const node of nodes) {
    if (node.id === id) return node
    const found = findNode(node.children, id)
    if (found) return found
  }
  return null
}

function subtreeIds(node: TreeNode): Set<string> {
  const ids = new Set<string>()
  const stack: TreeNode[] = [node]
  while (stack.length) {
    const current = stack.pop()!
    ids.add(current.id)
    current.children.forEach((c) => stack.push(c))
  }
  return ids
}

function move(dragId: string, dropId: string | null) {
  if (dragId === dropId) return

  const dragged = findNode(tree.value ?? [], dragId)
  if (!dragged) return

  if (dragged.parentId === dropId) return // already there — no-op

  if (dropId && subtreeIds(dragged).has(dropId)) {
    toast.add({
      severity: 'warn',
      summary: 'Invalid move',
      detail: 'A category cannot be moved into its own sub-tree.',
      life: 4000,
    })
    return
  }

  update.mutate(
    { id: dragId, body: { name: dragged.name, parentId: dropId } },
    {
      onSuccess: () =>
        toast.add({ severity: 'success', summary: 'Category moved', life: 2000 }),
      onError: (err) =>
        toast.add({ severity: 'error', summary: 'Move failed', detail: err.message, life: 5000 }),
    },
  )
}

function onRootDragOver(e: DragEvent) {
  if (!auth.canMutate || draggingId.value === null) return
  e.preventDefault()
  rootDragOver.value = true
}
function onRootDrop(e: DragEvent) {
  if (!auth.canMutate) return
  e.preventDefault()
  const dragId = draggingId.value
  if (dragId) move(dragId, null)
  draggingId.value = null
  dragOverId.value = null
  rootDragOver.value = false
}

// --- Create / rename dialog -------------------------------------------------
const dialogVisible = ref(false)
const dialogMode = ref<'create' | 'rename'>('create')
const dialogName = ref('')
const dialogParentId = ref<string | null>(null)
const dialogTargetId = ref<string | null>(null)
const dialogError = ref('')
const saving = computed(() => create.isPending.value || update.isPending.value)

function openCreateRoot() {
  dialogMode.value = 'create'
  dialogName.value = ''
  dialogParentId.value = null
  dialogTargetId.value = null
  dialogError.value = ''
  dialogVisible.value = true
}
function addChild(parentId: string) {
  dialogMode.value = 'create'
  dialogName.value = ''
  dialogParentId.value = parentId
  dialogTargetId.value = null
  dialogError.value = ''
  dialogVisible.value = true
}
function rename(node: TreeNode) {
  dialogMode.value = 'rename'
  dialogName.value = node.name
  dialogParentId.value = node.parentId
  dialogTargetId.value = node.id
  dialogError.value = ''
  dialogVisible.value = true
}

function submitDialog() {
  const name = dialogName.value.trim()
  if (!name) {
    dialogError.value = 'Name is required'
    return
  }
  dialogError.value = ''

  if (dialogMode.value === 'create') {
    create.mutate(
      { name, parentId: dialogParentId.value },
      {
        onSuccess: () => {
          dialogVisible.value = false
          toast.add({ severity: 'success', summary: 'Category created', life: 2000 })
        },
        onError: (err) => {
          dialogError.value = err.message
        },
      },
    )
  } else {
    update.mutate(
      { id: dialogTargetId.value!, body: { name, parentId: dialogParentId.value } },
      {
        onSuccess: () => {
          dialogVisible.value = false
          toast.add({ severity: 'success', summary: 'Category renamed', life: 2000 })
        },
        onError: (err) => {
          dialogError.value = err.message
        },
      },
    )
  }
}

// --- Delete -----------------------------------------------------------------
function remove(node: TreeNode) {
  confirm.require({
    message:
      `Delete "${node.name}"? Its sub-categories move up to its parent and its ` +
      `products become uncategorized. This cannot be undone.`,
    header: 'Delete category',
    icon: 'pi pi-exclamation-triangle',
    rejectProps: { label: 'Cancel', severity: 'secondary', text: true },
    acceptProps: { label: 'Delete', severity: 'danger' },
    accept: () =>
      del.mutate(node.id, {
        onSuccess: () =>
          toast.add({ severity: 'success', summary: 'Category deleted', life: 2000 }),
        onError: (err) =>
          toast.add({ severity: 'error', summary: 'Delete failed', detail: err.message, life: 5000 }),
      }),
  })
}

// --- View in catalog --------------------------------------------------------
function view(node: TreeNode) {
  router.push({ name: 'products', query: { categoryId: node.id } })
}

provide(categoryTreeKey, {
  canMutate: auth.canMutate,
  draggingId,
  dragOverId,
  isCollapsed,
  toggle,
  addChild,
  rename,
  remove,
  view,
  move,
})
</script>

<template>
  <section class="p-6 flex flex-col gap-4" style="max-width: 900px">
    <PageHeader title="Product Categories" subtitle="Organize products into a category tree">
      <template #title-actions>
        <RefreshButton :loading="isFetching" @click="() => refetch()" />
      </template>
      <template #actions>
        <Button
          v-if="auth.canMutate"
          label="Add root category"
          icon="pi pi-plus"
          @click="openCreateRoot"
        />
      </template>
    </PageHeader>

    <p v-if="auth.canMutate" class="text-sm text-surface-500 -mt-2">
      Drag a category onto another to move it; use the buttons on each row to add, rename, delete,
      or jump to its products.
    </p>

    <div v-if="isLoading" class="flex justify-center py-10">
      <ProgressSpinner />
    </div>

    <Message v-else-if="isError" severity="error" :closable="false">
      {{ error?.message ?? 'Failed to load categories.' }}
    </Message>

    <div
      v-else-if="isEmpty"
      class="rounded-xl border border-dashed border-surface-300 bg-white p-10 text-center text-surface-500"
    >
      No categories yet.
      <template v-if="auth.canMutate"> Use “Add root category” to create the first one.</template>
    </div>

    <div v-else class="rounded-xl border border-surface-200 bg-white p-2">
      <ul>
        <CategoryTreeNode
          v-for="node in tree"
          :key="node.id"
          :node="node"
          :depth="0"
        />
      </ul>
    </div>

    <!-- Drop zone to promote a category to the top level. -->
    <div
      v-if="auth.canMutate && draggingId"
      class="rounded-lg border-2 border-dashed p-4 text-center text-sm transition-colors"
      :class="rootDragOver ? 'border-primary-400 bg-primary-50 text-primary-700' : 'border-surface-300 text-surface-500'"
      @dragover="onRootDragOver"
      @dragleave="rootDragOver = false"
      @drop="onRootDrop"
    >
      Drop here to make it a top-level category
    </div>

    <Dialog
      v-model:visible="dialogVisible"
      :header="dialogMode === 'create' ? 'New category' : 'Rename category'"
      modal
      :style="{ width: '24rem' }"
    >
      <div class="flex flex-col gap-1">
        <label for="categoryName" class="text-sm font-medium text-surface-700">Name</label>
        <InputText
          id="categoryName"
          v-model="dialogName"
          autofocus
          fluid
          :invalid="!!dialogError"
          @keyup.enter="submitDialog"
        />
        <small v-if="dialogError" class="text-red-500">{{ dialogError }}</small>
      </div>
      <template #footer>
        <Button label="Cancel" text severity="secondary" @click="dialogVisible = false" />
        <Button label="Save" icon="pi pi-check" :loading="saving" @click="submitDialog" />
      </template>
    </Dialog>
  </section>
</template>
