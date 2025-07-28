<script setup lang="ts">
import { type Dto } from '@/DataTransfer.ts'
import EventView from './EventView.vue'

defineProps<{
  events: Dto.Event[]
}>()
</script>
<template>
  <InfoBar v-if="events.length === 0">Keine Events vorhanden.</InfoBar>
  <div v-else class="flex flex-col gap-2">
    <div v-for="event in events">
      <EventView :event="event" />
    </div>
  </div>
</template>