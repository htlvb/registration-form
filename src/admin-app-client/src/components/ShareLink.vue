<script setup lang="ts">
import { ref, useId, useTemplateRef } from 'vue'

const props = defineProps<{
  url: string
  title: string
}>()

const shareData = () : ShareData => ({ title: props.title, url: props.url })
const canShare = typeof navigator.share === 'function' && navigator.canShare?.(shareData()) !== false

const delay = (milliseconds: number) => new Promise(resolve => setTimeout(resolve, milliseconds))

const copyState = ref<'copying' | 'copied'>()
const copy = async () => {
  if (copyState.value !== undefined) return // still copying or showing the result

  copyState.value = 'copying'
  try {
    await navigator.clipboard.writeText(props.url)
    copyState.value = 'copied'
    await delay(2000)
    copyState.value = undefined
  }
  catch {
    copyState.value = undefined
    showUrlPopover()
  }
}

// last resort if the clipboard isn't accessible (e.g. no permission, insecure context): let the user copy the url manually
const anchorName = `--share-link-${useId().replace(/[^\w-]/g, '-')}` // unique per instance, must be a valid dashed ident
const urlPopover = useTemplateRef('url-popover')
const showUrlPopover = () => {
  if (urlPopover.value === null) return

  urlPopover.value.showPopover()
  window.getSelection()?.selectAllChildren(urlPopover.value) // ready for Ctrl+C
}

// share dialog of the OS (copy link, QR code, AirDrop, ...) isn't available everywhere (e.g. Linux, Firefox desktop)
const share = async () => {
  if (!canShare) {
    await copy()
    return
  }
  try {
    await navigator.share(shareData())
  }
  catch (e) {
    if (e instanceof DOMException && e.name === 'AbortError') return // dialog closed by user
    await copy()
  }
}
</script>

<template>
  <a class="p-2 cursor-pointer"
    :style="{ anchorName }"
    :class="{ 'text-green-400': copyState === 'copied' }"
    :title="copyState === 'copied' ? 'Link kopiert' : canShare ? 'Link teilen' : 'Link kopieren'"
    :href="url"
    @click.prevent="share">
    <i class="fa-solid" :class="{ 'fa-share-nodes': copyState !== 'copied' && canShare, 'fa-link': copyState !== 'copied' && !canShare, 'fa-check': copyState === 'copied' }"></i>
  </a>
  <!-- without anchor positioning support the popover stays centered in the viewport (browser default) -->
  <span ref="url-popover" popover="auto"
    class="text-sm p-2 rounded border shadow-md bg-white text-stone-800 dark:bg-stone-800 dark:text-stone-100 select-all
      supports-[position-area:top]:inset-auto supports-[position-area:top]:m-0 supports-[position-area:top]:my-2 [position-area:top] [position-try-fallbacks:flip-block]"
    :style="{ positionAnchor: anchorName }">{{ url }}</span>
</template>
