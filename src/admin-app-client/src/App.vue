<script setup lang="ts">
import { ref } from 'vue'
import LoginInformation from './components/LoginInformation.vue'
import LoadingBar from './components/LoadingBar.vue'
import ErrorWithRetry from './components/ErrorWithRetry.vue'
import { uiFetchAuthorized } from './UIFetch'
import * as DataTransfer from './DataTransfer'
import EventList from './EventList.vue'
import EventView from './EventView.vue'

const isLoadingEvents = ref(false)
const hasLoadingEventsFailed = ref(false)
const eventList = ref<DataTransfer.EventList>()
const loadEvents = async () => {
  const result = await uiFetchAuthorized(isLoadingEvents, hasLoadingEventsFailed, "/api/events")
  if (result.succeeded) {
    eventList.value = await result.response.json() as DataTransfer.EventList
  }
}
loadEvents()

const newEvent = ref<DataTransfer.Event>()
const addEvent = (createEventUrl: string) => {
  newEvent.value = {
    type: 'draft',
    key: '',
    title: '',
    infoText: '',
    reservationStartTime: '',
    slots: [],
    registrationConfirmationMail: { subject: '', contentTemplate: '' },
    requestConfirmationMail: null,
    url: createEventUrl,
    publicRegistrationUrl: '',
    canDelete: false,
    canEditEventData: true,
    canAddSlot: true,
    canEditSlot: true,
    canDeleteSlot: true,
    canViewRegistrations: false,
  }
}
</script>

<template>
  <header class="bg-blue-htlvb text-slate-300">
    <div class="container mx-auto flex flex-col sm:flex-row gap-2 sm:gap-6 my-4 px-4">
      <div>
        <img src="@/assets/logo.svg" class="h-[80px]" />
      </div>
      <div class="grow flex flex-col gap-2">
        <span class="text-2xl small-caps">Eventregistrierung</span>
        <span class="text-4xl small-caps">Administration</span>
      </div>
      <LoginInformation class="self-end" />
    </div>
  </header>

  <main class="container mx-auto">
    <section class="flex flex-col gap-4 mx-4 my-2">
      <LoadingBar v-if="isLoadingEvents"></LoadingBar>
      <ErrorWithRetry v-else-if="hasLoadingEventsFailed" @retry="loadEvents">Fehler beim Laden der Events.</ErrorWithRetry>
      <template v-else-if="eventList !== undefined">
        <section class="flex flex-col gap-2 border rounded px-4 py-2">
          <div class="flex items-stretch gap-2">
            <h2 class="text-2xl">Entwürfe</h2>
            <a v-if="eventList.createEventUrl !== null && newEvent === undefined" class="px-2 content-center cursor-pointer" title="Event hinzufügen" @click="addEvent(eventList.createEventUrl)">
              <i class="fa-solid fa-plus"></i>
            </a>
          </div>
          <EventView v-if="newEvent !== undefined" :event="newEvent" is-new @edit-cancelled="newEvent = undefined" @event-updated="newEvent = undefined; loadEvents()" />
          <EventList :events="eventList.events.filter(v => v.type === 'draft')" @event-updated="loadEvents" />
        </section>
        <section class="flex flex-col gap-2 border rounded px-4 py-2">
          <h2 class="text-2xl">Aktive Events</h2>
          <EventList :events="eventList.events.filter(v => v.type === 'released')" @event-updated="loadEvents" />
        </section>
        <section class="flex flex-col gap-2 border rounded px-4 py-2">
          <h2 class="text-2xl">Archiv</h2>
          <EventList :events="eventList.events.filter(v => v.type === 'archived')" @event-updated="loadEvents" />
        </section>
      </template>
    </section>
  </main>
</template>
