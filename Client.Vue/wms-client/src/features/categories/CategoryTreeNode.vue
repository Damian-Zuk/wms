<script setup lang="ts">
import { computed, inject } from 'vue'
import Button from 'primevue/button'
import type { CategoryTreeNode } from '@/types/categories'
import { categoryTreeKey } from './category-tree-context'

const props = defineProps<{ node: CategoryTreeNode; depth: number }>()

const ctx = inject(categoryTreeKey)!

const hasChildren = computed(() => props.node.children.length > 0)
const expanded = computed(() => !ctx.isCollapsed(props.node.id))
const isDragging = computed(() => ctx.draggingId.value === props.node.id)
const isDropTarget = computed(
  () =>
    ctx.dragOverId.value === props.node.id &&
    ctx.draggingId.value !== null &&
    ctx.draggingId.value !== props.node.id,
)

function onDragStart(e: DragEvent) {
  if (!ctx.canMutate) return
  e.stopPropagation()
  ctx.draggingId.value = props.node.id
  e.dataTransfer?.setData('text/plain', props.node.id)
  if (e.dataTransfer) e.dataTransfer.effectAllowed = 'move'
}

function onDragOver(e: DragEvent) {
  if (!ctx.canMutate || ctx.draggingId.value === null) return
  e.preventDefault()
  e.stopPropagation()
  ctx.dragOverId.value = props.node.id
  if (e.dataTransfer) e.dataTransfer.dropEffect = 'move'
}

function onDrop(e: DragEvent) {
  if (!ctx.canMutate) return
  e.preventDefault()
  e.stopPropagation()
  const dragId = ctx.draggingId.value ?? e.dataTransfer?.getData('text/plain') ?? null
  if (dragId) ctx.move(dragId, props.node.id)
  ctx.draggingId.value = null
  ctx.dragOverId.value = null
}

function onDragEnd() {
  ctx.draggingId.value = null
  ctx.dragOverId.value = null
}
</script>

<template>
  <li>
    <div
      class="flex items-center gap-2 py-1.5 pr-2 rounded-md border border-transparent transition-colors group hover:bg-surface-100"
      :class="{
        'opacity-40': isDragging,
        '!border-primary-400 bg-primary-50': isDropTarget,
        'cursor-grab': ctx.canMutate,
      }"
      :style="{ paddingLeft: depth * 1.5 + 0.5 + 'rem' }"
      :draggable="ctx.canMutate"
      @dragstart="onDragStart"
      @dragover="onDragOver"
      @drop="onDrop"
      @dragend="onDragEnd"
    >
      <button
        v-if="hasChildren"
        type="button"
        class="w-5 h-5 flex items-center justify-center text-surface-500 hover:text-surface-900"
        :aria-label="expanded ? 'Collapse' : 'Expand'"
        @click="ctx.toggle(node.id)"
      >
        <i :class="expanded ? 'pi pi-chevron-down' : 'pi pi-chevron-right'" class="text-xs" />
      </button>
      <span v-else class="w-5" />

      <i :class="hasChildren ? 'pi pi-folder' : 'pi pi-tag'" class="text-surface-400 text-sm" />
      <span class="font-medium text-surface-800">{{ node.name }}</span>

      <span
        class="text-xs text-surface-500 bg-surface-100 group-hover:bg-surface-200 rounded-full px-2 py-0.5"
        :title="`${node.directSkuCount} directly in this category, ${node.totalSkuCount} including sub-categories`"
      >
        {{ node.totalSkuCount }} SKU{{ node.totalSkuCount === 1 ? '' : 's' }}
      </span>

      <div class="ml-auto flex items-center gap-0.5 opacity-0 group-hover:opacity-100 focus-within:opacity-100">
        <Button
          icon="pi pi-filter"
          text
          rounded
          size="small"
          title="View products in catalog"
          aria-label="View products in catalog"
          @click="ctx.view(node)"
        />
        <template v-if="ctx.canMutate">
          <Button
            icon="pi pi-plus"
            text
            rounded
            size="small"
            title="Add sub-category"
            aria-label="Add sub-category"
            @click="ctx.addChild(node.id)"
          />
          <Button
            icon="pi pi-pencil"
            text
            rounded
            size="small"
            title="Rename"
            aria-label="Rename"
            @click="ctx.rename(node)"
          />
          <Button
            icon="pi pi-trash"
            text
            rounded
            size="small"
            severity="danger"
            title="Delete"
            aria-label="Delete"
            @click="ctx.remove(node)"
          />
        </template>
      </div>
    </div>

    <ul v-if="hasChildren && expanded">
      <CategoryTreeNode
        v-for="child in node.children"
        :key="child.id"
        :node="child"
        :depth="depth + 1"
      />
    </ul>
  </li>
</template>
